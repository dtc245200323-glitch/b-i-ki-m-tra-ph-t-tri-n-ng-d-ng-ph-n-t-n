using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
namespace Supportly.Core;

public class Store
{
    readonly string connection;
    public Store(string path) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); connection = new SqliteConnectionStringBuilder { DataSource=path, DefaultTimeout=15 }.ToString(); }
    public SqliteConnection Open() { var c = new SqliteConnection(connection); c.Open(); return c; }
    static SqliteCommand Cmd(SqliteConnection c, string sql, params object?[] args) { var cmd=c.CreateCommand();cmd.CommandText=sql; for(int i=0;i<args.Length;i++)cmd.Parameters.AddWithValue("$"+i,args[i]??DBNull.Value);return cmd; }
    static string Now()=>DateTimeOffset.UtcNow.ToString("O");
    static string Id()=>Guid.NewGuid().ToString("N");
    public void Initialize()
    {
        using var c=Open(); using var cmd=Cmd(c,"""
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,name TEXT NOT NULL,email TEXT NOT NULL UNIQUE,role TEXT NOT NULL,password TEXT NOT NULL,created TEXT NOT NULL,enabled INTEGER NOT NULL DEFAULT 1);
        CREATE TABLE IF NOT EXISTS tokens(token TEXT PRIMARY KEY,user_id TEXT NOT NULL,expires TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS conversations(id TEXT PRIMARY KEY,customer TEXT NOT NULL,subject TEXT NOT NULL,status TEXT NOT NULL,agent TEXT,created TEXT NOT NULL,closed TEXT);
        CREATE TABLE IF NOT EXISTS messages(id TEXT PRIMARY KEY,conversation TEXT NOT NULL,sender TEXT NOT NULL,name TEXT NOT NULL,text TEXT NOT NULL,sent TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_messages ON messages(conversation,sent);
        CREATE TABLE IF NOT EXISTS outbox(id INTEGER PRIMARY KEY AUTOINCREMENT,event_id TEXT UNIQUE NOT NULL,payload TEXT NOT NULL,sent INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS history_events(event_id TEXT PRIMARY KEY,type TEXT NOT NULL,conversation TEXT NOT NULL,payload TEXT NOT NULL,processed TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS history_messages(id TEXT PRIMARY KEY,conversation TEXT NOT NULL,sender TEXT NOT NULL,name TEXT NOT NULL,text TEXT NOT NULL,sent TEXT NOT NULL);
        """);cmd.ExecuteNonQuery();
    }
    public static string Hash(string password) { var salt=RandomNumberGenerator.GetBytes(16);return Convert.ToBase64String(salt)+":"+Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,salt,100000,HashAlgorithmName.SHA256,32)); }
    static bool Verify(string password,string encoded) {var parts=encoded.Split(':');return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]),Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),100000,HashAlgorithmName.SHA256,32));}
    public User Register(string name,string email,string password,string role="Customer")
    {
        name=name.Trim();email=email.Trim().ToLowerInvariant();
        if(name.Length is <2 or >80 || email.Length>160 || !System.Net.Mail.MailAddress.TryCreate(email,out var address) || address.Address!=email || password.Length is <8 or >128)throw new DomainException("Tên 2-80 ký tự, email hợp lệ và mật khẩu 8-128 ký tự.");
        var u=new User(Id(),name,email,role,Now(),true);using var c=Open();using var cmd=Cmd(c,"INSERT INTO users VALUES($0,$1,$2,$3,$4,$5,1)",u.Id,u.Name,u.Email,u.Role,Hash(password),u.CreatedAt);
        try{cmd.ExecuteNonQuery();}catch(SqliteException e)when(e.SqliteErrorCode==19){throw new DomainException("Email đã được sử dụng.",409);}return u;
    }
    static User ReadUser(SqliteDataReader r)=>new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5)==1);
    public List<User> Users(){using var c=Open();using var cmd=Cmd(c,"SELECT id,name,email,role,created,enabled FROM users ORDER BY created");using var r=cmd.ExecuteReader();var list=new List<User>();while(r.Read())list.Add(ReadUser(r));return list;}
    public (string Token,User User) Login(string email,string password)
    {
        using var c=Open();using var cmd=Cmd(c,"SELECT id,name,email,role,created,enabled,password FROM users WHERE email=$0",email.Trim().ToLowerInvariant());using var r=cmd.ExecuteReader();
        if(!r.Read()||!r.GetBoolean(5)||!Verify(password,r.GetString(6)))throw new DomainException("Email hoặc mật khẩu không đúng.",401);
        var user=ReadUser(r);r.Close();var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));using var ins=Cmd(c,"INSERT INTO tokens VALUES($0,$1,$2)",token,user.Id,DateTimeOffset.UtcNow.AddHours(12).ToString("O"));ins.ExecuteNonQuery();return(token,user);
    }
    public User? Authenticate(string token){using var c=Open();using var cmd=Cmd(c,"SELECT u.id,u.name,u.email,u.role,u.created,u.enabled FROM users u JOIN tokens t ON u.id=t.user_id WHERE t.token=$0 AND t.expires>$1 AND u.enabled=1",token,Now());using var r=cmd.ExecuteReader();return r.Read()?ReadUser(r):null;}
    public void Logout(string token){using var c=Open();using var cmd=Cmd(c,"DELETE FROM tokens WHERE token=$0",token);cmd.ExecuteNonQuery();}
    public DateTimeOffset? Expiry(string token){using var c=Open();using var cmd=Cmd(c,"SELECT expires FROM tokens WHERE token=$0",token);return cmd.ExecuteScalar() is string value?DateTimeOffset.Parse(value):null;}
    public void Toggle(string id,string current){if(id==current)throw new DomainException("Không thể khóa tài khoản đang đăng nhập.");using var c=Open();using var cmd=Cmd(c,"UPDATE users SET enabled=1-enabled WHERE id=$0",id);if(cmd.ExecuteNonQuery()==0)throw new DomainException("Không tìm thấy người dùng.",404);}
    const string SelectCon="SELECT c.id,c.customer,u.name,c.subject,c.status,c.agent,a.name,c.created,c.closed FROM conversations c JOIN users u ON c.customer=u.id LEFT JOIN users a ON c.agent=a.id";
    static Conversation ReadCon(SqliteDataReader r)=>new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.GetString(7),r.IsDBNull(8)?null:r.GetString(8));
    public List<Conversation> Conversations(User u){using var c=Open();using var cmd=Cmd(c,SelectCon+(u.Role=="Customer"?" WHERE c.customer=$0":"")+" ORDER BY c.created DESC",u.Id);using var r=cmd.ExecuteReader();var list=new List<Conversation>();while(r.Read())list.Add(ReadCon(r));return list;}
    public Conversation Get(string id,User u){using var c=Open();using var cmd=Cmd(c,SelectCon+" WHERE c.id=$0",id);using var r=cmd.ExecuteReader();if(!r.Read())throw new DomainException("Không tìm thấy phiên hỗ trợ.",404);var conv=ReadCon(r);if(u.Role=="Customer"&&conv.CustomerId!=u.Id)throw new DomainException("Không có quyền truy cập phiên này.",403);return conv;}
    static void Event(SqliteConnection c,SqliteTransaction tx,ChatEvent e){using var cmd=Cmd(c,"INSERT INTO outbox(event_id,payload) VALUES($0,$1)",e.EventId,JsonSerializer.Serialize(e));cmd.Transaction=tx;cmd.ExecuteNonQuery();}
    public Conversation Create(User u,string subject){subject=subject.Trim();if(subject.Length is <3 or >160)throw new DomainException("Chủ đề cần từ 3 đến 160 ký tự.");var id=Id();using var c=Open();using var tx=c.BeginTransaction();using var cmd=Cmd(c,"INSERT INTO conversations VALUES($0,$1,$2,'Waiting',NULL,$3,NULL)",id,u.Id,subject,Now());cmd.Transaction=tx;cmd.ExecuteNonQuery();Event(c,tx,new(Id(),"conversation.created",1,id,Now(),null));tx.Commit();return Get(id,u);}
    public Conversation Accept(string id,User u){if(u.Role!="Agent")throw new DomainException("Chỉ tư vấn viên được nhận phiên.",403);Get(id,u);using var c=Open();using var cmd=Cmd(c,"UPDATE conversations SET agent=$0,status='Active' WHERE id=$1 AND status='Waiting'",u.Id,id);if(cmd.ExecuteNonQuery()==0)throw new DomainException("Phiên đã được nhận hoặc đã đóng.",409);return Get(id,u);}
    public Conversation Close(string id,User u){var conv=Get(id,u);if(u.Role=="Agent"&&conv.AgentId!=u.Id)throw new DomainException("Bạn chưa được phân công phiên này.",403);using var c=Open();using var tx=c.BeginTransaction();using var cmd=Cmd(c,"UPDATE conversations SET status='Closed',closed=$0 WHERE id=$1 AND status!='Closed'",Now(),id);cmd.Transaction=tx;if(cmd.ExecuteNonQuery()==0)throw new DomainException("Phiên đã đóng.",409);Event(c,tx,new(Id(),"conversation.closed",1,id,Now(),null));tx.Commit();return Get(id,u);}
    public ChatMessage Send(string id,User u,string text,string clientId)
    {
        text=text.Trim();if(text.Length is <1 or >4000 || !Guid.TryParse(clientId,out _))throw new DomainException("Tin nhắn cần từ 1 đến 4000 ký tự và mã GUID hợp lệ.");
        using var c=Open();using var tx=c.BeginTransaction();using var check=Cmd(c,"SELECT customer,status,agent FROM conversations WHERE id=$0",id);check.Transaction=tx;using(var r=check.ExecuteReader()){if(!r.Read())throw new DomainException("Không tìm thấy phiên.",404);if(r.GetString(0)!=u.Id && (r.IsDBNull(2)||r.GetString(2)!=u.Id))throw new DomainException("Bạn không phải người tham gia.",403);if(r.GetString(1)=="Closed")throw new DomainException("Phiên đã đóng.",409);}
        var m=new ChatMessage(clientId,id,u.Id,u.Name,text,Now());using var cmd=Cmd(c,"INSERT OR IGNORE INTO messages VALUES($0,$1,$2,$3,$4,$5)",m.Id,id,u.Id,u.Name,text,m.SentAt);cmd.Transaction=tx;
        if(cmd.ExecuteNonQuery()==0){using var prior=Cmd(c,"SELECT id,conversation,sender,name,text,sent FROM messages WHERE id=$0",clientId);prior.Transaction=tx;using var r=prior.ExecuteReader();r.Read();var old=ReadMessage(r);if(old.ConversationId!=id||old.SenderId!=u.Id||old.Text!=text)throw new DomainException("Mã tin nhắn đã được sử dụng.",409);return old;}
        Event(c,tx,new(Id(),"message.sent",1,id,m.SentAt,m));tx.Commit();return m;
    }
    static ChatMessage ReadMessage(SqliteDataReader r)=>new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5));
    public List<ChatMessage> Messages(string id,User u,bool history=false){Get(id,u);using var c=Open();using var cmd=Cmd(c,$"SELECT id,conversation,sender,name,text,sent FROM {(history?"history_messages":"messages")} WHERE conversation=$0 ORDER BY sent,id",id);using var r=cmd.ExecuteReader();var list=new List<ChatMessage>();while(r.Read())list.Add(ReadMessage(r));return list;}
    public List<(long Id,string Payload)> Pending(){using var c=Open();using var cmd=Cmd(c,"SELECT id,payload FROM outbox WHERE sent=0 ORDER BY id LIMIT 100");using var r=cmd.ExecuteReader();var list=new List<(long,string)>();while(r.Read())list.Add((r.GetInt64(0),r.GetString(1)));return list;}
    public void Published(long id){using var c=Open();using var cmd=Cmd(c,"UPDATE outbox SET sent=1 WHERE id=$0",id);cmd.ExecuteNonQuery();}
    public bool Archive(ChatEvent e){using var c=Open();using var tx=c.BeginTransaction();using var cmd=Cmd(c,"INSERT OR IGNORE INTO history_events VALUES($0,$1,$2,$3,$4)",e.EventId,e.EventType,e.ConversationId,JsonSerializer.Serialize(e),Now());cmd.Transaction=tx;bool inserted=cmd.ExecuteNonQuery()>0;if(inserted&&e.Message is {} m){using var ins=Cmd(c,"INSERT OR IGNORE INTO history_messages VALUES($0,$1,$2,$3,$4,$5)",m.Id,m.ConversationId,m.SenderId,m.SenderName,m.Text,m.SentAt);ins.Transaction=tx;ins.ExecuteNonQuery();}tx.Commit();return inserted;}
    public object Metrics(){using var c=Open();var values=new Dictionary<string,long>();foreach(var (name,sql) in new[]{("pendingEvents","SELECT COUNT(*) FROM outbox WHERE sent=0"),("archivedEvents","SELECT COUNT(*) FROM history_events"),("archivedMessages","SELECT COUNT(*) FROM history_messages")}){using var cmd=Cmd(c,sql);values[name]=(long)cmd.ExecuteScalar()!;}return values;}
}
