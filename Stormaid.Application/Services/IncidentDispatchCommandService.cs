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


    public async Task<IncidentDispatchResponse> AcceptAsync(
        long dispatchId,
        long responderUserId,
        CancellationToken cancellationToken = default)
    {
        if (dispatchId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchId));
        }

        if (responderUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responderUserId));
        }

        return await _transactionExecutor.ExecuteAsync(
            async token =>
            {
                var dispatch = await _dispatchRepository.GetTrackedByIdAsync(
                    dispatchId,
                    token);

                if (dispatch is null)
                {
                    throw new KeyNotFoundException("Dispatch was not found.");
                }

                // 01 - Repetição pelo mesmo responsável não altera o histórico.
                if (dispatch.Status == DispatchStatus.ACCEPTED)
                {
                    dispatch.Accept(responderUserId, DateTime.UtcNow);
                    return IncidentDispatchResponse.FromEntity(dispatch);
                }

                // 02 - A aceitação exige vínculo ativo com a unidade designada.
                var assigned = await _dispatchRepository.HasActiveAssignmentAsync(
                    dispatch.RescueUnitId,
                    responderUserId,
                    token);

                if (!assigned)
                {
                    throw new UnauthorizedAccessException(
                        "Responder is not actively assigned to this rescue unit.");
                }

                var previousStatus = dispatch.Status;
                var utcNow = DateTime.UtcNow;

                dispatch.Accept(responderUserId, utcNow);

                var history = new DispatchStatusHistory(
                    incidentDispatchId: dispatch.Id,
                    previousStatus: previousStatus,
                    newStatus: DispatchStatus.ACCEPTED,
                    changedByUserId: responderUserId,
                    changedByRole: DispatchActorRole.RESPONDER,
                    note: "Despacho aceito pela equipe de resgate.",
                    utcNow: utcNow);

                await _dispatchRepository.AddHistoryAsync(history, token);
                await _dispatchRepository.SaveChangesAsync(token);

                return IncidentDispatchResponse.FromEntity(dispatch);
            },
            cancellationToken);
    }

    /// <summary>
    /// Inicia o deslocamento da unidade e publica o status público EN_ROUTE.
    /// A atualização do despacho, histórico, versão pública e Outbox é atômica.
    /// </summary>
    public async Task<IncidentDispatchResponse> StartRouteAsync(
        long dispatchId,
        long responderUserId,
        CancellationToken cancellationToken = default)
    {
        if (dispatchId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchId));
        }

        if (responderUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responderUserId));
        }

        return await _transactionExecutor.ExecuteAsync(
            async token =>
            {
                var dispatch = await _dispatchRepository.GetTrackedByIdAsync(
                    dispatchId,
                    token);

                if (dispatch is null)
                {
                    throw new KeyNotFoundException("Dispatch was not found.");
                }

                // O deslocamento é exclusivo do profissional que aceitou o despacho.
                if (dispatch.AcceptedByResponderUserId != responderUserId)
                {
                    throw new UnauthorizedAccessException(
                        "Responder did not accept this dispatch.");
                }

                var assigned = await _dispatchRepository.HasActiveAssignmentAsync(
                    dispatch.RescueUnitId,
                    responderUserId,
                    token);

                if (!assigned)
                {
                    throw new UnauthorizedAccessException(
                        "Responder is not actively assigned to this rescue unit.");
                }

                // Uma repetição pelo mesmo profissional não cria histórico ou evento.
                if (dispatch.Status == DispatchStatus.EN_ROUTE)
                {
                    return IncidentDispatchResponse.FromEntity(dispatch);
                }

                if (dispatch.Status != DispatchStatus.ACCEPTED)
                {
                    throw new InvalidOperationException(
                        "Dispatch is not accepted.");
                }

                var incident = await _incidentRepository.GetTrackedByIdAsync(
                    dispatch.IncidentReferenceId,
                    token);

                if (incident is null)
                {
                    throw new KeyNotFoundException("Incident was not found.");
                }

                if (incident.RegulationStatus != RegulationStatus.DISPATCHED)
                {
                    throw new InvalidOperationException(
                        "Incident is not dispatched.");
                }

                var utcNow = DateTime.UtcNow;
                var previousStatus = dispatch.Status;

                dispatch.StartRoute(utcNow);
                incident.AdvancePublicStatusVersion(utcNow);

                var history = new DispatchStatusHistory(
                    incidentDispatchId: dispatch.Id,
                    previousStatus: previousStatus,
                    newStatus: DispatchStatus.EN_ROUTE,
                    changedByUserId: responderUserId,
                    changedByRole: DispatchActorRole.RESPONDER,
                    note: "Equipe iniciou o deslocamento para a ocorrência.",
                    utcNow: utcNow);

                await _dispatchRepository.AddHistoryAsync(history, token);

                // EN_ROUTE é um status público do Java, não de RegulationStatus.
                var eventId = $"evt-dispatch-{incident.JavaIncidentId}-{incident.PublicStatusVersion}";
                var payload = JsonSerializer.Serialize(new
                {
                    eventType = StatusChangedEventType,
                    eventId,
                    incidentId = incident.JavaIncidentId,
                    status = DispatchStatus.EN_ROUTE.ToString(),
                    version = incident.PublicStatusVersion,
                    changedAt = utcNow
                });

                await _outbox.EnqueueAsync(
                    eventId: eventId,
                    eventType: StatusChangedEventType,
                    routingKey: StatusChangedEventType,
                    payload: payload,
                    cancellationToken: token);

                // O executor transacional confirma todas as alterações juntas.
                return IncidentDispatchResponse.FromEntity(dispatch);
            },
            cancellationToken);
    }


    /// <summary>
    /// Registra a chegada da equipe ao local e publica o status ON_SITE.
    /// Preserva a situação regulatória DISPATCHED no .NET.
    /// </summary>
    public async Task<IncidentDispatchResponse> ArriveAsync(
        long dispatchId,
        long responderUserId,
        CancellationToken cancellationToken = default)
    {
        if (dispatchId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchId));
        }

        if (responderUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responderUserId));
        }

        return await _transactionExecutor.ExecuteAsync(
            async token =>
            {
                var dispatch = await _dispatchRepository.GetTrackedByIdAsync(
                    dispatchId,
                    token);

                if (dispatch is null)
                {
                    throw new KeyNotFoundException("Dispatch was not found.");
                }

                if (dispatch.AcceptedByResponderUserId != responderUserId)
                {
                    throw new UnauthorizedAccessException(
                        "Responder did not accept this dispatch.");
                }

                var assigned = await _dispatchRepository.HasActiveAssignmentAsync(
                    dispatch.RescueUnitId,
                    responderUserId,
                    token);

                if (!assigned)
                {
                    throw new UnauthorizedAccessException(
                        "Responder is not actively assigned to this rescue unit.");
                }

                // Repetições pelo responsável não geram novos eventos ou histórico.
                if (dispatch.Status == DispatchStatus.ON_SITE)
                {
                    return IncidentDispatchResponse.FromEntity(dispatch);
                }

                if (dispatch.Status != DispatchStatus.EN_ROUTE)
                {
                    throw new InvalidOperationException(
                        "Dispatch is not en route.");
                }

                var incident = await _incidentRepository.GetTrackedByIdAsync(
                    dispatch.IncidentReferenceId,
                    token);

                if (incident is null)
                {
                    throw new KeyNotFoundException("Incident was not found.");
                }

                if (incident.RegulationStatus != RegulationStatus.DISPATCHED)
                {
                    throw new InvalidOperationException(
                        "Incident is not dispatched.");
                }

                var utcNow = DateTime.UtcNow;
                var previousStatus = dispatch.Status;

                dispatch.Arrive(utcNow);
                incident.AdvancePublicStatusVersion(utcNow);

                var history = new DispatchStatusHistory(
                    incidentDispatchId: dispatch.Id,
                    previousStatus: previousStatus,
                    newStatus: DispatchStatus.ON_SITE,
                    changedByUserId: responderUserId,
                    changedByRole: DispatchActorRole.RESPONDER,
                    note: "Equipe chegou ao local da ocorrência.",
                    utcNow: utcNow);

                await _dispatchRepository.AddHistoryAsync(history, token);

                var eventId = $"evt-dispatch-{incident.JavaIncidentId}-{incident.PublicStatusVersion}";
                var payload = JsonSerializer.Serialize(new
                {
                    eventType = StatusChangedEventType,
                    eventId,
                    incidentId = incident.JavaIncidentId,
                    status = DispatchStatus.ON_SITE.ToString(),
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

    /// <summary>
    /// Conclui o atendimento, libera a unidade e publica RESOLVED no Java.
    /// A regulação é encerrada internamente como CLOSED.
    /// </summary>
    public async Task<IncidentDispatchResponse> ResolveAsync(
        long dispatchId,
        long responderUserId,
        CancellationToken cancellationToken = default)
    {
        if (dispatchId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchId));
        }

        if (responderUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responderUserId));
        }

        return await _transactionExecutor.ExecuteAsync(
            async token =>
            {
                var dispatch = await _dispatchRepository.GetTrackedByIdAsync(
                    dispatchId,
                    token);

                if (dispatch is null)
                {
                    throw new KeyNotFoundException("Dispatch was not found.");
                }

                if (dispatch.AcceptedByResponderUserId != responderUserId)
                {
                    throw new UnauthorizedAccessException(
                        "Responder did not accept this dispatch.");
                }

                var assigned = await _dispatchRepository.HasActiveAssignmentAsync(
                    dispatch.RescueUnitId,
                    responderUserId,
                    token);

                if (!assigned)
                {
                    throw new UnauthorizedAccessException(
                        "Responder is not actively assigned to this rescue unit.");
                }

                // A repetição não libera novamente a unidade nem recria histórico ou evento.
                if (dispatch.Status == DispatchStatus.RESOLVED)
                {
                    return IncidentDispatchResponse.FromEntity(dispatch);
                }

                if (dispatch.Status != DispatchStatus.ON_SITE)
                {
                    throw new InvalidOperationException(
                        "Dispatch is not on site.");
                }

                var incident = await _incidentRepository.GetTrackedByIdAsync(
                    dispatch.IncidentReferenceId,
                    token);

                if (incident is null)
                {
                    throw new KeyNotFoundException("Incident was not found.");
                }

                if (incident.RegulationStatus != RegulationStatus.DISPATCHED)
                {
                    throw new InvalidOperationException(
                        "Incident is not dispatched.");
                }

                var unit = await _rescueUnitRepository.GetTrackedByIdAsync(
                    dispatch.RescueUnitId,
                    token);

                if (unit is null)
                {
                    throw new KeyNotFoundException("Rescue unit was not found.");
                }

                // Não reativar inadvertidamente unidades inativas ou fora de serviço.
                if (!unit.Active ||
                    unit.OperationalStatus != UnitOperationalStatus.BUSY)
                {
                    throw new InvalidOperationException(
                        "Rescue unit is not active and busy.");
                }

                var utcNow = DateTime.UtcNow;
                var previousStatus = dispatch.Status;

                dispatch.Resolve(utcNow);
                incident.Close(utcNow);
                incident.AdvancePublicStatusVersion(utcNow);
                unit.MarkAsAvailable(utcNow);

                var history = new DispatchStatusHistory(
                    incidentDispatchId: dispatch.Id,
                    previousStatus: previousStatus,
                    newStatus: DispatchStatus.RESOLVED,
                    changedByUserId: responderUserId,
                    changedByRole: DispatchActorRole.RESPONDER,
                    note: "Atendimento concluído pela equipe de resgate.",
                    utcNow: utcNow);

                await _dispatchRepository.AddHistoryAsync(history, token);

                // RESOLVED é o status público; CLOSED é apenas o estado interno .NET.
                var eventId = $"evt-dispatch-{incident.JavaIncidentId}-{incident.PublicStatusVersion}";
                var payload = JsonSerializer.Serialize(new
                {
                    eventType = StatusChangedEventType,
                    eventId,
                    incidentId = incident.JavaIncidentId,
                    status = DispatchStatus.RESOLVED.ToString(),
                    version = incident.PublicStatusVersion,
                    changedAt = utcNow
                });

                await _outbox.EnqueueAsync(
                    eventId: eventId,
                    eventType: StatusChangedEventType,
                    routingKey: StatusChangedEventType,
                    payload: payload,
                    cancellationToken: token);

                // O executor confirma despacho, ocorrência, unidade, histórico e Outbox juntos.
                return IncidentDispatchResponse.FromEntity(dispatch);
            },
            cancellationToken);
    }
}
