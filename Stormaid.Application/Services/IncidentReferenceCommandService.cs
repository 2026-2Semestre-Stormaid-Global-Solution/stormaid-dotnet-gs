
using System.Text.Json;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Services;

/// <summary>
/// Representa o resultado do início da análise
/// institucional de uma ocorrência.
/// </summary>
public sealed record IncidentReviewResult(
    long JavaIncidentId,
    RegulationStatus RegulationStatus,
    long PublicStatusVersion,
    long? ReviewedByRegulatorUserId,
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
    /// Estado atualizado, autoria original da análise
    /// e indicação de transição realizada.
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

        // 1 - Retornar o estado existente sem alterar
        // a autoria ou gerar um novo evento.

        if (incident.RegulationStatus == RegulationStatus.UNDER_REVIEW)
        {
            return new IncidentReviewResult(
                incident.JavaIncidentId,
                incident.RegulationStatus,
                incident.PublicStatusVersion,
                incident.ReviewedByRegulatorUserId,
                Changed: false
            );
        }

        // 2 - Impedir transições inválidas.

        if (incident.RegulationStatus != RegulationStatus.PENDING_REVIEW)
        {
            throw new InvalidOperationException(
                $"Cannot begin review from {incident.RegulationStatus}."
            );
        }

        var utcNow = DateTime.UtcNow;

        // 3 - Iniciar a análise e registrar o regulador
        // responsável pela primeira transição.

        incident.BeginReview(regulatorUserId, utcNow);
        incident.AdvancePublicStatusVersion(utcNow);

        // 4 - Preparar o evento de atualização
        // pública para integração com a API Java.

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

        // 5 - Adicionar o evento à Outbox
        // antes de solicitar a persistência.

        await _outbox.EnqueueAsync(
            eventId: eventId,
            eventType: StatusChangedEventType,
            routingKey: StatusChangedEventType,
            payload: payload,
            cancellationToken: cancellationToken
        );

        // 6 - Persistir as alterações da ocorrência
        // e a mensagem da Outbox.

        await _repository.SaveChangesAsync(cancellationToken);

        // 7 - Retornar o estado atualizado
        // com a autoria registrada.

        return new IncidentReviewResult(
            incident.JavaIncidentId,
            incident.RegulationStatus,
            incident.PublicStatusVersion,
            incident.ReviewedByRegulatorUserId,
            Changed: true
        );
    }
}
