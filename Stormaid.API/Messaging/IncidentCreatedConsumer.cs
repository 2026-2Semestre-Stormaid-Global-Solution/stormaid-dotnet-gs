
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
    /// <param name="scopeFactory">Fábrica de escopos para os serviços da aplicação.</param>
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval =
                        TimeSpan.FromSeconds(5),
                    Ssl = new SslOption
                    {
                        Enabled = _options.UseTls,
                        ServerName = _options.HostName
                    }
                };

                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using var channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken
                    );

                // Verifica se a fila já existe no broker.
                await channel.QueueDeclarePassiveAsync(
                    _options.QueueName,
                    stoppingToken
                );

                // Processa uma mensagem por vez.
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken
                );

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var message =
                            JsonSerializer.Deserialize<IncidentCreatedEvent>(
                                delivery.Body.ToArray(),
                                JsonOptions
                            );

                        if (message is null)
                        {
                            throw new JsonException("Evento de ocorrência vazio.");
                        }

                        await using var scope =
                            _scopeFactory.CreateAsyncScope();

                        var service = scope.ServiceProvider
                            .GetRequiredService<IncidentCreatedEventService>();

                        var incident = await service.ProcessAsync(
                            message,
                            stoppingToken
                        );

                        // Confirma somente após o processamento.
                        await channel.BasicAckAsync(
                            delivery.DeliveryTag,
                            multiple: false,
                            cancellationToken: stoppingToken
                        );

                        _logger.LogInformation(
                            "Ocorrência Java {IncidentId} processada.",
                            incident.JavaIncidentId
                        );
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        // Sem ACK: o broker poderá reenviar
                        // a mensagem após o encerramento.
                    }
                    catch (Exception ex)
                        when (ex is JsonException or ArgumentException)
                    {
                        _logger.LogError(
                            ex,
                            "Evento inválido recebido do Java."
                        );

                        // Exige Dead Letter Queue antes de habilitar.
                        await channel.BasicNackAsync(
                            delivery.DeliveryTag,
                            multiple: false,
                            requeue: false
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Falha ao processar ocorrência."
                        );

                        // Atraso para reduzir reentregas imediatas.
                        await Task.Delay(
                            TimeSpan.FromSeconds(3),
                            stoppingToken
                        );

                        await channel.BasicNackAsync(
                            delivery.DeliveryTag,
                            multiple: false,
                            requeue: true,
                            cancellationToken: stoppingToken
                        );
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation(
                    "Consumindo eventos da fila {QueueName}.",
                    _options.QueueName
                );

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
                    "Falha na conexão com RabbitMQ."
                );

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
