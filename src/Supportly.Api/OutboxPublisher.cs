using Supportly.Core;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
class OutboxPublisher(Store store,IConfiguration config,ILogger<OutboxPublisher> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                var factory=Broker.Factory(config["RabbitMQ:Host"]??"localhost",config["RabbitMQ:User"]??"supportly",config["RabbitMQ:Password"]??"supportly-dev");
                await using var conn=await factory.CreateConnectionAsync(ct);
                await using var ch=await conn.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled:true,publisherConfirmationTrackingEnabled:true),ct);
                await Broker.Setup(ch,ct);
                while(!ct.IsCancellationRequested&&ch.IsOpen)
                {
                    foreach(var (id,payload) in store.Pending())
                    {
                        var e=JsonSerializer.Deserialize<ChatEvent>(payload)!;
                        await ch.BasicPublishAsync(Broker.Exchange,e.EventType,true,new BasicProperties{Persistent=true,ContentType="application/json",MessageId=e.EventId,Type=e.EventType},Encoding.UTF8.GetBytes(payload),ct);
                        store.Published(id);log.LogInformation("Published {EventId} {Type}",e.EventId,e.EventType);
                    }
                    await Task.Delay(500,ct);
                }
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e){log.LogWarning("RabbitMQ unavailable; durable outbox retained: {Reason}",e.Message);await Task.Delay(5000,ct);}
        }
    }
}
