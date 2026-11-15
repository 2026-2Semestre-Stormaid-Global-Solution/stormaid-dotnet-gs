
using System.Text.Json;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Services;

/// <summary>
/// Resultado da tentativa de iniciar a análise
/// institucional de uma ocorrência.
/// </summary>
public sealed record IncidentReviewResult(
    long JavaIncidentId,
    RegulationStatus RegulationStatus,
    long PublicStatusVersion,
    bool Changed
);

/// <summary>
/// Executa operações que modificam o estado
/// das ocorrências recebidas da API Java.
/// </summary>
public sealed class IncidentReferenceCommandService
{
    private const string StatusChangedEventType =
        "incident.status.changed.v1";

    private readonly IIncidentReferenceRepository _repository;
    private readonly IIntegrationEventOutbox _outbox;

    /// <summary>
    /// Inicializa o serviço de comandos de regulação.
    /// </summary>
    public IncidentReferenceCommandService(
        IIncidentReferenceRepository repository,
        IIntegrationEventOutbox outbox)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _outbox = outbox
            ?? throw new ArgumentNullException(nameof(outbox));
    }

    /// <summary>
    /// Inicia a análise de uma ocorrência e registra
    /// o evento de atualização pública na Outbox.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador original da ocorrência no Java.
    /// </param>
    /// <param name="regulatorUserId">
    /// Identificador do regulador autenticado, obtido do JWT.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Estado atualizado e indicação de transição realizada.
    /// </returns>
    public async Task<IncidentReviewResult> BeginReviewAsync(
        long javaIncidentId,
        long regulatorUserId,
        CancellationToken cancellationToken = default)
    {
        if (javaIncidentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(javaIncidentId)
            );
        }

        if (regulatorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(regulatorUserId)
            );
        }

        var incident = await _repository
            .GetTrackedByJavaIncidentIdAsync(
                javaIncidentId,
                cancellationToken
            );

        if (incident is null)
        {
            throw new KeyNotFoundException(
                $"Incident {javaIncidentId} was not found."
            );
        }

        if (incident.RegulationStatus == RegulationStatus.UNDER_REVIEW)
        {
            return new IncidentReviewResult(
                incident.JavaIncidentId,
                incident.RegulationStatus,
                incident.PublicStatusVersion,
                Changed: false
            );
        }

        if (incident.RegulationStatus != RegulationStatus.PENDING_REVIEW)
        {
            throw new InvalidOperationException(
                $"Cannot begin review from {incident.RegulationStatus}."
            );
        }

        var utcNow = DateTime.UtcNow;

        incident.BeginReview(regulatorUserId, utcNow);
        incident.AdvancePublicStatusVersion(utcNow);

        var eventId =
            $"evt-regulation-{javaIncidentId}-{incident.PublicStatusVersion}";

        var payload = JsonSerializer.Serialize(new
        {
            eventType = StatusChangedEventType,
            eventId,
            incidentId = javaIncidentId,
            status = RegulationStatus.UNDER_REVIEW.ToString(),
            version = incident.PublicStatusVersion,
            changedAt = utcNow
        });

        await _outbox.EnqueueAsync(
            eventId: eventId,
            eventType: StatusChangedEventType,
            routingKey: StatusChangedEventType,
            payload: payload,
            cancellationToken: cancellationToken
        );

        await _repository.SaveChangesAsync(cancellationToken);

        return new IncidentReviewResult(
            incident.JavaIncidentId,
            incident.RegulationStatus,
            incident.PublicStatusVersion,
            Changed: true
        );
    }
}
