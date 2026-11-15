using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stormaid.Application.Interfaces;

namespace Stormaid.API.Messaging;

/// <summary>
/// Processa mensagens pendentes da Outbox
/// e publica eventos de integração no RabbitMQ.
/// </summary>
public sealed class OutboxPublisherBackgroundService
    : BackgroundService
{
    private const int BatchSize = 10;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    /// <summary>
    /// Inicializa o serviço de publicação da Outbox.
    /// </summary>
    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Consulta periodicamente mensagens pendentes,
    /// enquanto o serviço estiver em execução.
    /// </summary>
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Processador de Outbox iniciado."
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
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
                    "Falha ao consultar ou processar a Outbox."
                );
            }

            try
            {
                await Task.Delay(
                    PollInterval,
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Processador de Outbox encerrado."
        );
    }

    /// <summary>
    /// Processa um lote de mensagens ainda não publicadas.
    /// </summary>
    private async Task ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IOutboxMessageRepository>();

        var publisher = scope.ServiceProvider
            .GetRequiredService<IOutboxEventPublisher>();

        var messages = await repository.GetPendingBatchAsync(
            BatchSize,
            cancellationToken
        );

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1 - Enviar o evento ao RabbitMQ.
            try
            {
                await publisher.PublishAsync(
                    message.EventId,
                    message.EventType,
                    message.RoutingKey,
                    message.Payload,
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha na publicação do evento {EventId}.",
                    message.EventId
                );

                try
                {
                    await repository.RegisterFailureAsync(
                        message.Id,
                        ex.GetType().Name,
                        cancellationToken
                    );
                }
                catch (Exception persistenceException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(
                        persistenceException,
                        "Falha ao registrar tentativa do evento {EventId}.",
                        message.EventId
                    );
                }

                continue;
            }

            // 2 - Registrar a publicação após confirmação.
            try
            {
                await repository.MarkPublishedAsync(
                    message.Id,
                    cancellationToken
                );

                _logger.LogInformation(
                    "Evento {EventId} marcado como publicado.",
                    message.EventId
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Evento {EventId} enviado, mas não confirmado no Oracle.",
                    message.EventId
                );
            }
        }
    }
}
