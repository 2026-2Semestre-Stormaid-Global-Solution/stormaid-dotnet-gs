using System.Text.Json;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Services;

public sealed class IncidentDispatchCommandService
{
    private const string StatusChangedEventType = "incident.status.changed.v1";

    private readonly IIncidentReferenceRepository _incidentRepository;
    private readonly IRegulationDecisionRepository _decisionRepository;
    private readonly IRescueUnitRepository _rescueUnitRepository;
    private readonly IIncidentDispatchRepository _dispatchRepository;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ITransactionExecutor _transactionExecutor;

    public IncidentDispatchCommandService(
        IIncidentReferenceRepository incidentRepository,
        IRegulationDecisionRepository decisionRepository,
        IRescueUnitRepository rescueUnitRepository,
        IIncidentDispatchRepository dispatchRepository,
        IIntegrationEventOutbox outbox,
        ITransactionExecutor transactionExecutor)
    {
        _incidentRepository = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));

        _decisionRepository = decisionRepository ?? throw new ArgumentNullException(nameof(decisionRepository));

        _rescueUnitRepository = rescueUnitRepository ?? throw new ArgumentNullException(nameof(rescueUnitRepository));

        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));

        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));

        _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
    }

    public async Task<IncidentDispatchResponse> CreateAsync(
        CreateIncidentDispatchRequest request,
        long regulatorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.JavaIncidentId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.JavaIncidentId));
        }

        if (request.RescueUnitId <= 0)
        {
            throw new ArgumentOutOfRangeException( nameof(request.RescueUnitId));
        }

        if (regulatorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException( nameof(regulatorUserId));
        }

        return await _transactionExecutor.ExecuteAsync(
            async token =>
            {
                var incident = await _incidentRepository
                    .GetTrackedByJavaIncidentIdAsync(
                        request.JavaIncidentId,
                        token);

                if (incident is null)
                {
                    throw new KeyNotFoundException("Incident was not found.");
                }

                if (incident.RegulationStatus !=
                    RegulationStatus.AWAITING_DISPATCH)
                {
                    throw new InvalidOperationException("Incident is not awaiting dispatch.");
                }

                var decision = await _decisionRepository
                    .GetLatestByIncidentReferenceIdAsync(
                        incident.Id,
                        token);

                if (decision is null ||
                    decision.DecisionType !=
                        RegulationDecisionType.DISPATCH_APPROVED ||
                    !decision.InstitutionType.HasValue)
                {
                    throw new InvalidOperationException("Incident does not have an approved dispatch decision.");
                }

                var activeDispatch = await _dispatchRepository
                    .GetActiveByIncidentReferenceIdAsync(
                        incident.Id,
                        token);

                if (activeDispatch is not null)
                {
                    throw new InvalidOperationException("Incident already has an active dispatch.");
                }

                var unit = await _rescueUnitRepository
                    .GetTrackedByIdAsync(
                        request.RescueUnitId,
                        token);

                if (unit is null)
                {
                    throw new KeyNotFoundException("Rescue unit was not found.");
                }

                if (!unit.Active ||
                    unit.OperationalStatus !=
                        UnitOperationalStatus.AVAILABLE)
                {
                    throw new InvalidOperationException( "Rescue unit is not available.");
                }

                if (unit.InstitutionType != decision.InstitutionType.Value)
                {
                    throw new InvalidOperationException( "Rescue unit institution does not match the approved decision.");
                }

                var utcNow = DateTime.UtcNow;

                var dispatch = new IncidentDispatch(
                    incidentReferenceId: incident.Id,
                    regulationDecisionId: decision.Id,
                    rescueUnitId: unit.Id,
                    createdByRegulatorUserId: regulatorUserId,
                    notes: request.Notes,
                    utcNow: utcNow);

                await _dispatchRepository.AddAsync(
                    dispatch,
                    token);

                unit.MarkAsBusy(utcNow);

                incident.MarkDispatched(utcNow);
                incident.AdvancePublicStatusVersion(utcNow);

                await _dispatchRepository.SaveChangesAsync(token);

                if (dispatch.Id <= 0)
                {
                    throw new InvalidOperationException("Dispatch identifier was not generated.");
                }

                var history = new DispatchStatusHistory(
                    incidentDispatchId: dispatch.Id,
                    previousStatus: null,
                    newStatus: DispatchStatus.PENDING_ACCEPTANCE,
                    changedByUserId: regulatorUserId,
                    changedByRole: DispatchActorRole.REGULATOR,
                    note: "Despacho operacional criado.",
                    utcNow: utcNow);

                await _dispatchRepository.AddHistoryAsync(
                    history,
                    token);

                var eventId = $"evt-dispatch-{incident.JavaIncidentId}-{incident.PublicStatusVersion}";

                var payload = JsonSerializer.Serialize(new
                {
                    eventType = StatusChangedEventType,
                    eventId,
                    incidentId = incident.JavaIncidentId,
                    status = incident.RegulationStatus.ToString(),
                    version = incident.PublicStatusVersion,
                    changedAt = utcNow
                });

                await _outbox.EnqueueAsync(
                    eventId: eventId,
                    eventType: StatusChangedEventType,
                    routingKey: StatusChangedEventType,
                    payload: payload,
                    cancellationToken: token);

                return IncidentDispatchResponse.FromEntity(dispatch);
            },
            cancellationToken);
    }
}
