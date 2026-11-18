using RabbitMQ.Client;
namespace Supportly.Core;
public static class Broker
{
    public const string Exchange="supportly.events", Queue="supportly.history", Dead="supportly.dead";
    public static ConnectionFactory Factory(string host,string user,string password)=>new(){HostName=host,UserName=user,Password=password,AutomaticRecoveryEnabled=true,RequestedConnectionTimeout=TimeSpan.FromSeconds(5)};
    public static async Task Setup(IChannel ch,CancellationToken ct)
    {
        await ch.ExchangeDeclareAsync(Exchange,ExchangeType.Topic,true,false,cancellationToken:ct);
        await ch.ExchangeDeclareAsync(Dead,ExchangeType.Fanout,true,false,cancellationToken:ct);
        await ch.QueueDeclareAsync(Dead,true,false,false,cancellationToken:ct);
        await ch.QueueBindAsync(Dead,Dead,"",cancellationToken:ct);
        await ch.QueueDeclareAsync(Queue,true,false,false,new Dictionary<string,object?>{{"x-dead-letter-exchange",Dead}},cancellationToken:ct);
        await ch.QueueBindAsync(Queue,Exchange,"#",cancellationToken:ct);
    }
}
