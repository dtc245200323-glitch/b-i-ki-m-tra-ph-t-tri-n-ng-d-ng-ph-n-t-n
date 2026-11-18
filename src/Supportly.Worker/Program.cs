using Supportly.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Store(builder.Configuration["DataPath"]??"data/supportly.db"));
builder.Services.AddSingleton<WorkerState>();builder.Services.AddHostedService<HistoryWorker>();
var app=builder.Build();app.Services.GetRequiredService<Store>().Initialize();
app.MapGet("/health",(WorkerState state)=>Results.Json(new{status=state.Connected?"ready":"waiting-for-broker",state.Processed},statusCode:state.Connected?200:503));app.Run();
class WorkerState{public volatile bool Connected;public long Processed;}
class HistoryWorker(Store store,IConfiguration config,ILogger<HistoryWorker> log,WorkerState state):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                var f=Broker.Factory(config["RabbitMQ:Host"]??"localhost",config["RabbitMQ:User"]??"supportly",config["RabbitMQ:Password"]??"supportly-dev");
                await using var conn=await f.CreateConnectionAsync(ct);await using var ch=await conn.CreateChannelAsync(cancellationToken:ct);await Broker.Setup(ch,ct);await ch.BasicQosAsync(0,20,false,ct);
                var consumer=new AsyncEventingBasicConsumer(ch);
                consumer.ReceivedAsync+=async(sender,ea)=>
                {
                    ChatEvent? e;
                    try{e=JsonSerializer.Deserialize<ChatEvent>(ea.Body.Span);if(e==null||e.Version!=1||!Guid.TryParse(e.EventId,out _)||string.IsNullOrEmpty(e.ConversationId)||e.EventType is not ("message.sent" or "conversation.created" or "conversation.closed")||(e.EventType=="message.sent"&&e.Message==null))throw new JsonException("Invalid event contract");}
                    catch(JsonException ex){log.LogWarning("Rejected invalid event: {Reason}",ex.Message);await ch.BasicNackAsync(ea.DeliveryTag,false,false,ct);return;}
                    try{var added=store.Archive(e);if(added)Interlocked.Increment(ref state.Processed);await ch.BasicAckAsync(ea.DeliveryTag,false,ct);log.LogInformation("Archived {EventId} {Type}; inserted={Inserted}",e.EventId,e.EventType,added);}
                    catch(Exception ex){log.LogError(ex,"Archive failed; retry after backoff");await Task.Delay(2000,ct);await ch.BasicNackAsync(ea.DeliveryTag,false,true,ct);}
                };
                await ch.BasicConsumeAsync(Broker.Queue,false,consumer,ct);state.Connected=true;
                while(ch.IsOpen&&!ct.IsCancellationRequested)await Task.Delay(1000,ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){log.LogWarning("History worker reconnecting: {Reason}",ex.Message);state.Connected=false;await Task.Delay(5000,ct);}
            finally{state.Connected=false;}
        }
    }
}
