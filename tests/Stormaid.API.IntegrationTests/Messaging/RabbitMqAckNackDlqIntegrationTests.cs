using System.Text;
using RabbitMQ.Client;

namespace Stormaid.API.IntegrationTests.Messaging;

/// <summary>
/// Valida ACK, NACK com requeue e dead lettering em um broker RabbitMQ local,
/// sem iniciar a API e sem acessar o Oracle.
/// </summary>
public sealed class RabbitMqAckNackDlqIntegrationTests
{
    private const string RoutingKey = "incident.status.changed.v1";
    private const string DeadRoutingKey = "incident.status.changed.dead";

    [RabbitMqFact]
    public async Task Ack_ShouldRemoveMessageFromOriginalQueue()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = await CreateConnectionAsync(timeout.Token);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: timeout.Token);

        var exchange = $"stormaid.lab.ack.{Guid.NewGuid():N}";
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: true,
            cancellationToken: timeout.Token);

        var queue = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: timeout.Token);

        await channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchange,
            routingKey: RoutingKey,
            cancellationToken: timeout.Token);

        var payload = """{"status":"CANCELLED","version":5}""";
        await PublishAsync(channel, exchange, payload, timeout.Token);

        var delivery = await WaitForMessageAsync(
            channel, queue.QueueName, timeout.Token);

        Assert.Equal(payload, Encoding.UTF8.GetString(delivery.Body.ToArray()));

        await channel.BasicAckAsync(
            delivery.DeliveryTag,
            multiple: false,
            cancellationToken: timeout.Token);

        var remaining = await channel.BasicGetAsync(
            queue.QueueName,
            autoAck: false,
            cancellationToken: timeout.Token);

        Assert.Null(remaining);
    }

    [RabbitMqFact]
    public async Task NackWithRequeue_ShouldReturnMessageToOriginalQueue()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = await CreateConnectionAsync(timeout.Token);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: timeout.Token);

        var exchange = $"stormaid.lab.requeue.{Guid.NewGuid():N}";
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: true,
            cancellationToken: timeout.Token);

        var queue = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: timeout.Token);

        await channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchange,
            routingKey: RoutingKey,
            cancellationToken: timeout.Token);

        var payload = """{"status":"CANCELLED","version":6}""";
        await PublishAsync(channel, exchange, payload, timeout.Token);

        var firstDelivery = await WaitForMessageAsync(
            channel, queue.QueueName, timeout.Token);

        await channel.BasicNackAsync(
            firstDelivery.DeliveryTag,
            multiple: false,
            requeue: true,
            cancellationToken: timeout.Token);

        var secondDelivery = await WaitForMessageAsync(
            channel, queue.QueueName, timeout.Token);

        Assert.Equal(payload, Encoding.UTF8.GetString(secondDelivery.Body.ToArray()));

        await channel.BasicAckAsync(
            secondDelivery.DeliveryTag,
            multiple: false,
            cancellationToken: timeout.Token);
    }

    [RabbitMqFact]
    public async Task NackWithoutRequeue_ShouldMoveMessageToDeadLetterQueue()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = await CreateConnectionAsync(timeout.Token);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: timeout.Token);

        var suffix = Guid.NewGuid().ToString("N");
        var exchange = $"stormaid.lab.main.{suffix}";
        var deadLetterExchange = $"stormaid.lab.dlx.{suffix}";

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: true,
            cancellationToken: timeout.Token);

        await channel.ExchangeDeclareAsync(
            exchange: deadLetterExchange,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: true,
            cancellationToken: timeout.Token);

        var deadLetterQueue = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: timeout.Token);

        await channel.QueueBindAsync(
            queue: deadLetterQueue.QueueName,
            exchange: deadLetterExchange,
            routingKey: DeadRoutingKey,
            cancellationToken: timeout.Token);

        var mainQueue = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = DeadRoutingKey
            },
            cancellationToken: timeout.Token);

        await channel.QueueBindAsync(
            queue: mainQueue.QueueName,
            exchange: exchange,
            routingKey: RoutingKey,
            cancellationToken: timeout.Token);

        var payload = """{"status":"CANCELLED","version":7}""";
        await PublishAsync(channel, exchange, payload, timeout.Token);

        var original = await WaitForMessageAsync(
            channel, mainQueue.QueueName, timeout.Token);

        await channel.BasicNackAsync(
            original.DeliveryTag,
            multiple: false,
            requeue: false,
            cancellationToken: timeout.Token);

        var dead = await WaitForMessageAsync(
            channel, deadLetterQueue.QueueName, timeout.Token);

        Assert.Equal(payload, Encoding.UTF8.GetString(dead.Body.ToArray()));
        Assert.True(dead.BasicProperties.Headers?.ContainsKey("x-death") == true);

        var remaining = await channel.BasicGetAsync(
            mainQueue.QueueName,
            autoAck: false,
            cancellationToken: timeout.Token);

        Assert.Null(remaining);

        await channel.BasicAckAsync(
            dead.DeliveryTag,
            multiple: false,
            cancellationToken: timeout.Token);
    }

    private static async Task<IConnection> CreateConnectionAsync(
        CancellationToken cancellationToken)
    {
        var user = Environment.GetEnvironmentVariable("STORMAID_RABBITMQ_USER");
        var password = Environment.GetEnvironmentVariable("STORMAID_RABBITMQ_PASSWORD");

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Configure STORMAID_RABBITMQ_USER e STORMAID_RABBITMQ_PASSWORD antes dos testes.");
        }

        var factory = new ConnectionFactory
        {
            HostName = "127.0.0.1",
            Port = 5673,
            VirtualHost = "/",
            UserName = user,
            Password = password,
            AutomaticRecoveryEnabled = false
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }

    private static async Task PublishAsync(
        IChannel channel,
        string exchange,
        string payload,
        CancellationToken cancellationToken)
    {
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                MessageId = $"evt-lab-{Guid.NewGuid():N}"
            },
            body: Encoding.UTF8.GetBytes(payload),
            cancellationToken: cancellationToken);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(
        IChannel channel,
        string queue,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var message = await channel.BasicGetAsync(
                queue,
                autoAck: false,
                cancellationToken: cancellationToken);

            if (message is not null)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new InvalidOperationException(
            $"Nenhuma mensagem recebida na fila temporária {queue}.");
    }
}
