using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Stormaid.Infrastructure.Messaging;

namespace Stormaid.API.IntegrationTests.Messaging;

public sealed class RabbitMqOutboxPublisherIntegrationTests
{
    private const string RoutingKey = "incident.status.changed.v1";

    [RabbitMqFact]
    public async Task PublishAsync_ShouldDeliverPersistentMessage()
    {
        var options = CreateOptions();
        var exchange = $"stormaid.test.{Guid.NewGuid():N}";

        using var timeout =
            new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var factory = CreateConnectionFactory(options);

        await using var connection =
            await factory.CreateConnectionAsync(timeout.Token);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: timeout.Token);

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
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

        options.StatusExchangeName = exchange;

        var publisher = new RabbitMqOutboxPublisher(
            Options.Create(options));

        var eventId = $"evt-lab-{Guid.NewGuid():N}";

        var payload = $$"""
        {
          "eventId": "{{eventId}}",
          "eventType": "incident.status.changed.v1",
          "incidentId": 900010,
          "status": "CANCELLED",
          "version": 5
        }
        """;

        await publisher.PublishAsync(
            eventId,
            RoutingKey,
            RoutingKey,
            payload,
            timeout.Token);

        var delivery = await channel.BasicGetAsync(
            queue.QueueName,
            autoAck: false,
            cancellationToken: timeout.Token);

        Assert.NotNull(delivery);

        Assert.Equal(eventId, delivery.BasicProperties.MessageId);
        Assert.Equal(RoutingKey, delivery.BasicProperties.Type);
        Assert.True(delivery.BasicProperties.Persistent);

        Assert.Equal(
            payload,
            Encoding.UTF8.GetString(delivery.Body.ToArray()));

        await channel.BasicAckAsync(
            delivery.DeliveryTag,
            multiple: false,
            cancellationToken: timeout.Token);
    }

    [RabbitMqFact]
    public async Task PublishAsync_ShouldRejectUnroutableMessage()
    {
        var options = CreateOptions();
        var exchange = $"stormaid.test.{Guid.NewGuid():N}";

        using var timeout =
            new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var factory = CreateConnectionFactory(options);

        await using var connection =
            await factory.CreateConnectionAsync(timeout.Token);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: timeout.Token);

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: false,
            autoDelete: true,
            cancellationToken: timeout.Token);

        options.StatusExchangeName = exchange;

        var publisher = new RabbitMqOutboxPublisher(
            Options.Create(options));

        await Assert.ThrowsAnyAsync<PublishException>(
            async () => await publisher.PublishAsync(
                $"evt-unroutable-{Guid.NewGuid():N}",
                RoutingKey,
                RoutingKey,
                """{"status":"CANCELLED"}""",
                timeout.Token));
    }

    private static RabbitMqOptions CreateOptions()
    {
        return new RabbitMqOptions
        {
            HostName = "127.0.0.1",
            Port = 5673,
            VirtualHost = "/",
            UserName = Environment.GetEnvironmentVariable(
                "STORMAID_RABBITMQ_USER") ?? "",
            Password = Environment.GetEnvironmentVariable(
                "STORMAID_RABBITMQ_PASSWORD") ?? "",
            UseTls = false
        };
    }

    private static ConnectionFactory CreateConnectionFactory(
        RabbitMqOptions options)
    {
        return new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.UserName,
            Password = options.Password,
            AutomaticRecoveryEnabled = false
        };
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(
                "STORMAID_RUN_RABBITMQ_TESTS") != "1")
        {
            Skip = "Teste RabbitMQ local não habilitado.";
        }
    }
}
