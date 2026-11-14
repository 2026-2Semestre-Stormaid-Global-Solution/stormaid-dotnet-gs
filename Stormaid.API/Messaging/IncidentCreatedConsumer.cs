
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Stormaid.Application.DTOs.Integration;
using Stormaid.Application.Services;
using Stormaid.Infrastructure.Messaging;

namespace Stormaid.API.Messaging;

/// <summary>
/// Consome eventos de ocorrências criadas pela API Java.
/// </summary>
public sealed class IncidentCreatedConsumer : BackgroundService
{
    private const int MaxProcessingAttempts = 3;

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentCreatedConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(
                    namingPolicy: null,
                    allowIntegerValues: false
                )
            }
        };

    /// <summary>
    /// Inicializa o consumidor de eventos de ocorrências criadas pelo Java.
    /// </summary>
    /// <param name="options">Configurações de conexão com RabbitMQ.</param>
    /// <param name="scopeFactory">Fábrica de escopos dos serviços da aplicação.</param>
    /// <param name="logger">Serviço responsável pelo registro de logs.</param>
    public IncidentCreatedConsumer(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<IncidentCreatedConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Consumidor RabbitMQ desabilitado."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "As credenciais do RabbitMQ não foram configuradas."
            );
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1 - Configurar a conexão com RabbitMQ

                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.UserName,
                    Password = _options.Password,

                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),

                    Ssl = new SslOption
                    {
                        Enabled = _options.UseTls,
                        ServerName = _options.HostName
                    }
                };

                // 2 - Criar conexão e canal

                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using var channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken
                    );

                // 3 - Verificar a existência da fila

                await channel.QueueDeclarePassiveAsync(
                    _options.QueueName,
                    stoppingToken
                );

                // 4 - Limitar mensagens pendentes de confirmação

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken
                );

                // 5 - Criar consumidor assíncrono

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    await ProcessDeliveryAsync(
                        channel,
                        delivery.Body.ToArray(),
                        delivery.DeliveryTag,
                        stoppingToken
                    );
                };

                // 6 - Registrar consumidor na fila

                await channel.BasicConsumeAsync(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation(
                    "Consumidor conectado à fila {QueueName}.",
                    _options.QueueName
                );

                // 7 - Manter o serviço ativo

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao estabelecer conexão com RabbitMQ."
                );

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation(
            "Consumidor RabbitMQ encerrado."
        );
    }

    /// <summary>
    /// Processa uma mensagem recebida do RabbitMQ e
    /// confirma ou rejeita a entrega conforme o resultado.
    /// </summary>
    internal async Task ProcessDeliveryAsync(
        IChannel channel,
        byte[] payload,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        IncidentCreatedEvent message;

        // 1 - Desserializar o evento recebido do Java

        try
        {
            message = JsonSerializer.Deserialize<IncidentCreatedEvent>(
                payload,
                JsonOptions
            ) ?? throw new JsonException(
                "Evento de ocorrência vazio."
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON inválido recebido da API Java."
            );

            await RejectToDlqAsync(
                channel,
                deliveryTag,
                cancellationToken
            );

            return;
        }

        // 2 - Executar até três tentativas de processamento

        for (var attempt = 1; attempt <= MaxProcessingAttempts; attempt++)
        {
            long javaIncidentId;

            try
            {
                // Novo escopo para cada tentativa.
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<IncidentCreatedEventService>();

                var incident = await service.ProcessAsync(
                    message,
                    cancellationToken
                );

                javaIncidentId = incident.JavaIncidentId;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Sem ACK: a mensagem poderá ser reentregue.
                return;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(
                    ex,
                    "Ocorrência Java {IncidentId} contém dados inválidos.",
                    message.IncidentId
                );

                await RejectToDlqAsync(
                    channel,
                    deliveryTag,
                    cancellationToken
                );

                return;
            }
            catch (Exception ex)
            {
                if (attempt == MaxProcessingAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Ocorrência Java {IncidentId} falhou após {MaxAttempts} tentativas.",
                        message.IncidentId,
                        MaxProcessingAttempts
                    );

                    await RejectToDlqAsync(
                        channel,
                        deliveryTag,
                        cancellationToken
                    );

                    return;
                }

                _logger.LogWarning(
                    ex,
                    "Falha na tentativa {Attempt}/{MaxAttempts} da ocorrência Java {IncidentId}.",
                    attempt,
                    MaxProcessingAttempts,
                    message.IncidentId
                );

                // Intervalos de 2 e 4 segundos.
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(attempt * 2),
                        cancellationToken
                    );
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                continue;
            }

            // 3 - Confirmar somente após a persistência

            try
            {
                await channel.BasicAckAsync(
                    deliveryTag,
                    multiple: false,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation(
                    "Ocorrência Java {IncidentId} processada e confirmada.",
                    javaIncidentId
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // O processamento terminou, mas o ACK pode
                // não ter chegado ao broker.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao confirmar a ocorrência Java {IncidentId} no RabbitMQ.",
                    javaIncidentId
                );

                // Não enviar NACK após uma tentativa de ACK:
                // o resultado da confirmação pode ser incerto.
                throw;
            }

            return;
        }
    }

    /// <summary>
    /// Rejeita uma mensagem sem recolocá-la na fila principal.
    /// A política de Dead Letter configurada no broker
    /// encaminha a mensagem para a DLQ.
    /// </summary>
    private async Task RejectToDlqAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        await channel.BasicNackAsync(
            deliveryTag,
            multiple: false,
            requeue: false,
            cancellationToken: cancellationToken
        );

        _logger.LogWarning(
            "Mensagem {DeliveryTag} rejeitada para encaminhamento à DLQ.",
            deliveryTag
        );
    }
}
