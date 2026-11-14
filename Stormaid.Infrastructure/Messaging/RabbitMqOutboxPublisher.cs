using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Stormaid.Application.Interfaces;

namespace Stormaid.Infrastructure.Messaging;

/// <summary>
/// Publica eventos da Outbox no RabbitMQ,
/// utilizando mensagens persistentes
/// e confirmações de publicação.
/// </summary>
public sealed class RabbitMqOutboxPublisher
    : IOutboxEventPublisher
{
    private readonly RabbitMqOptions _options;

    /// <summary>
    /// Inicializa o publicador com as configurações
    /// existentes de conexão RabbitMQ.
    /// </summary>
    public RabbitMqOutboxPublisher(
        IOptions<RabbitMqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <summary>
    /// Publica uma mensagem e aguarda a confirmação
    /// de recebimento pelo broker RabbitMQ.
    /// </summary>
    public async Task PublishAsync(
        string eventId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        ValidateConfiguration();

        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,

            // A Outbox controla novas tentativas.
            AutomaticRecoveryEnabled = false,

            Ssl = new SslOption
            {
                Enabled = _options.UseTls,
                ServerName = _options.HostName
            }
        };

        await using var connection =
            await factory.CreateConnectionAsync(timeout.Token);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true
        );

        await using var channel =
            await connection.CreateChannelAsync(
                options: channelOptions,
                cancellationToken: timeout.Token
            );

        // Confirma que o exchange esperado existe.
        await channel.ExchangeDeclarePassiveAsync(
            _options.StatusExchangeName,
            timeout.Token
        );

        var properties = new BasicProperties
        {
            MessageId = eventId,
            Type = eventType,
            ContentType = "application/json",
            Persistent = true
        };

        var body = Encoding.UTF8.GetBytes(payload);

        // Mandatory impede considerar uma mensagem
        // não roteada como publicação bem-sucedida.
        await channel.BasicPublishAsync(
            exchange: _options.StatusExchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: timeout.Token
        );
    }

    /// <summary>
    /// Verifica as configurações obrigatórias
    /// antes de tentar conectar ao RabbitMQ.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "As credenciais do RabbitMQ não foram configuradas."
            );
        }

        if (string.IsNullOrWhiteSpace(
                _options.StatusExchangeName))
        {
            throw new InvalidOperationException(
                "O exchange de status não foi configurado."
            );
        }
    }
}
