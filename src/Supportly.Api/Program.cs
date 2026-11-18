using Supportly.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Store(builder.Configuration["DataPath"]??"data/supportly.db"));
builder.Services.AddSignalR(o=>{o.MaximumReceiveMessageSize=32768;});
builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions,SessionAuth>("Session",null);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Connections>();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",_=>new(){PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddHostedService<OutboxPublisher>();
var app=builder.Build();
var store=app.Services.GetRequiredService<Store>();store.Initialize();
if(app.Configuration.GetValue<bool>("SeedDemo"))
    foreach(var (name,email,role) in new[]{("Quản trị viên","admin@supportly.local","Admin"),("Phạm Thu Hà","agent@supportly.local","Agent"),("Lê Khánh An","customer@supportly.local","Customer")})
        if(!store.Users().Any(u=>u.Email==email))store.Register(name,email,"Supportly123!",role);
app.Use(async(ctx,next)=>{try{await next();}catch(DomainException e){ctx.Response.StatusCode=e.Status;await ctx.Response.WriteAsJsonAsync(new{error=e.Message});}catch(Exception e){app.Logger.LogError(e,"Unhandled request error");ctx.Response.StatusCode=500;await ctx.Response.WriteAsJsonAsync(new{error="Có lỗi máy chủ. Vui lòng thử lại."});}});
app.UseDefaultFiles();app.UseStaticFiles();app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.MapGet("/health",()=>Results.Ok(new{service="Supportly.Api",status="running"}));
app.MapPost("/auth/register",(RegisterRequest r,Store s)=>Results.Created("/auth/me",s.Register(r.Name??"",r.Email??"",r.Password??""))).RequireRateLimiting("auth");
app.MapPost("/auth/login",(LoginRequest r,Store s)=>{var result=s.Login(r.Email??"",r.Password??"");return Results.Ok(new{token=result.Token,user=result.User});}).RequireRateLimiting("auth");
app.MapGet("/auth/me",(HttpContext c,Store s)=>Current(c,s)).RequireAuthorization();
app.MapPost("/auth/logout",(HttpContext c,Store s,Connections connections)=>{var token=SessionAuth.Token(c);s.Logout(token);connections.AbortToken(token);return Results.NoContent();}).RequireAuthorization();
app.MapGet("/conversations",(HttpContext c,Store s)=>s.Conversations(Current(c,s))).RequireAuthorization();
app.MapPost("/conversations",async(CreateRequest r,HttpContext c,Store s,IHubContext<ChatHub> hub)=>{var u=Current(c,s);if(u.Role!="Customer")throw new DomainException("Chỉ khách hàng được tạo yêu cầu hỗ trợ.",403);var conv=s.Create(u,r.Subject??"");await Changed(hub,conv);return Results.Created($"/conversations/{conv.Id}",conv);}).RequireAuthorization();
app.MapGet("/conversations/{id}",(string id,HttpContext c,Store s)=>s.Get(id,Current(c,s))).RequireAuthorization();
app.MapGet("/conversations/{id}/messages",(string id,HttpContext c,Store s)=>s.Messages(id,Current(c,s))).RequireAuthorization();
app.MapGet("/conversations/{id}/history",(string id,HttpContext c,Store s)=>s.Messages(id,Current(c,s),true)).RequireAuthorization();
app.MapPost("/conversations/{id}/accept",async(string id,HttpContext c,Store s,IHubContext<ChatHub> hub)=>{var conv=s.Accept(id,Current(c,s));await Changed(hub,conv);return conv;}).RequireAuthorization();
app.MapPost("/conversations/{id}/close",async(string id,HttpContext c,Store s,IHubContext<ChatHub> hub)=>{var conv=s.Close(id,Current(c,s));await hub.Clients.Group(id).SendAsync("ConversationClosed",id);await Changed(hub,conv);return conv;}).RequireAuthorization();
app.MapGet("/admin/users",(HttpContext c,Store s)=>{Admin(c,s);return s.Users();}).RequireAuthorization();
app.MapPost("/admin/users",(AdminUserRequest r,HttpContext c,Store s)=>{Admin(c,s);if(r.Role is not ("Customer" or "Agent"))throw new DomainException("Vai trò phải là Customer hoặc Agent.");return s.Register(r.Name??"",r.Email??"",r.Password??"",r.Role);}).RequireAuthorization();
app.MapPost("/admin/users/{id}/toggle",(string id,HttpContext c,Store s,Connections connections)=>{var u=Admin(c,s);s.Toggle(id,u.Id);connections.AbortUser(id);return Results.NoContent();}).RequireAuthorization();
app.MapGet("/admin/metrics",(HttpContext c,Store s)=>{Admin(c,s);return s.Metrics();}).RequireAuthorization();
app.MapHub<ChatHub>("/hubs/chat",options=>options.CloseOnAuthenticationExpiration=true).RequireAuthorization();
app.Run();
static User Current(HttpContext c,Store s)=>s.Authenticate(SessionAuth.Token(c))??throw new DomainException("Phiên đăng nhập đã hết hạn.",401);
static User Admin(HttpContext c,Store s){var u=Current(c,s);if(u.Role!="Admin")throw new DomainException("Chỉ quản trị viên được truy cập.",403);return u;}
static Task Changed(IHubContext<ChatHub> hub,Conversation c)=>Task.WhenAll(hub.Clients.Group("staff").SendAsync("ConversationChanged",c.Id),hub.Clients.User(c.CustomerId).SendAsync("ConversationChanged",c.Id));
record RegisterRequest(string? Name,string? Email,string? Password);
record LoginRequest(string? Email,string? Password);
record CreateRequest(string? Subject);
record AdminUserRequest(string? Name,string? Email,string? Password,string Role);

class SessionAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,Store store):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public static string Token(HttpContext ctx){var h=ctx.Request.Headers.Authorization.ToString();return h.StartsWith("Bearer ")?h[7..]:ctx.Request.Path.StartsWithSegments("/hubs/chat")?ctx.Request.Query["access_token"].ToString():"";}
    protected override Task<AuthenticateResult> HandleAuthenticateAsync(){var user=store.Authenticate(Token(Context));if(user==null)return Task.FromResult(AuthenticateResult.NoResult());var claims=new[]{new Claim(ClaimTypes.NameIdentifier,user.Id),new Claim(ClaimTypes.Name,user.Name),new Claim(ClaimTypes.Role,user.Role)};return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),new AuthenticationProperties{ExpiresUtc=store.Expiry(Token(Context))},Scheme.Name)));}
}
class ChatHub(Store store,Connections connections):Hub
{
    User Me()=>store.Authenticate(SessionAuth.Token(Context.GetHttpContext()!))??throw new HubException("Phiên đăng nhập đã hết hạn.");
    public override async Task OnConnectedAsync(){var u=Me();connections.Add(u.Id,SessionAuth.Token(Context.GetHttpContext()!),Context);if(u.Role is "Admin" or "Agent")await Groups.AddToGroupAsync(Context.ConnectionId,"staff");await base.OnConnectedAsync();}
    public override Task OnDisconnectedAsync(Exception? exception){connections.Remove(Context.ConnectionId);return base.OnDisconnectedAsync(exception);}
    public async Task JoinConversation(string id){try{var u=Me();var c=store.Get(id,u);if(u.Role=="Agent"&&c.AgentId!=u.Id)throw new DomainException("Hãy nhận phiên trước khi tham gia.",403);await Groups.AddToGroupAsync(Context.ConnectionId,id);}catch(DomainException e){throw new HubException(e.Message);}}
    public Task LeaveConversation(string id)=>Groups.RemoveFromGroupAsync(Context.ConnectionId,id);
    public async Task<ChatMessage> SendMessage(string id,string text,string clientId){try{var m=store.Send(id,Me(),text,clientId);await Clients.Group(id).SendAsync("MessageReceived",m);return m;}catch(DomainException e){throw new HubException(e.Message);}}
}
