using System.Text.Json;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Services;

public sealed record RegulationDecisionResult(
    long DecisionId,
    long JavaIncidentId,
    RegulationDecisionType DecisionType,
    ResponderInstitutionType? InstitutionType,
    RegulationStatus RegulationStatus,
    long PublicStatusVersion,
    long RegulatorUserId,
    bool Changed
);

public sealed class RegulationDecisionCommandService
{
    private const string StatusChangedEventType = "incident.status.changed.v1";

    private readonly IIncidentReferenceRepository _incidentRepository;
    private readonly IRegulationDecisionRepository _decisionRepository;
    private readonly IIntegrationEventOutbox _outbox;

    public RegulationDecisionCommandService(
        IIncidentReferenceRepository incidentRepository,
        IRegulationDecisionRepository decisionRepository,
        IIntegrationEventOutbox outbox)
    {
        _incidentRepository = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));

        _decisionRepository = decisionRepository ?? throw new ArgumentNullException(nameof(decisionRepository));

        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    public async Task<RegulationDecisionResult> RegisterAsync(
        long javaIncidentId,
        long regulatorUserId,
        RegulationDecisionType decisionType,
        ResponderInstitutionType? institutionType,
        string justification,
        CancellationToken cancellationToken = default)
    {
        if (javaIncidentId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(javaIncidentId));
        }

        if (regulatorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regulatorUserId));
        }

        var incident = await _incidentRepository
            .GetTrackedByJavaIncidentIdAsync(
                javaIncidentId,
                cancellationToken);

        if (incident is null)
        {
            throw new KeyNotFoundException($"Incident {javaIncidentId} was not found.");
        }

        var latestDecision = await _decisionRepository
            .GetLatestByIncidentReferenceIdAsync(
                incident.Id,
                cancellationToken);

        ArgumentException.ThrowIfNullOrWhiteSpace(justification);

        var normalizedJustification = justification.Trim();

        var expectedStatus = decisionType switch
        {
            RegulationDecisionType.DISPATCH_APPROVED => RegulationStatus.AWAITING_DISPATCH,

            RegulationDecisionType.NO_DISPATCH_REQUIRED => RegulationStatus.CLOSED,

            RegulationDecisionType.MORE_INFORMATION_REQUIRED => RegulationStatus.UNDER_REVIEW,

            _ => (RegulationStatus?)null
        };

        if (latestDecision is not null &&
            expectedStatus.HasValue &&
            incident.RegulationStatus == expectedStatus.Value &&
            latestDecision.DecisionType == decisionType &&
            latestDecision.InstitutionType == institutionType &&
            latestDecision.Justification == normalizedJustification)
        {
            return new RegulationDecisionResult(
                DecisionId: latestDecision.Id,
                JavaIncidentId: incident.JavaIncidentId,
                DecisionType: latestDecision.DecisionType,
                InstitutionType: latestDecision.InstitutionType,
                RegulationStatus: incident.RegulationStatus,
                PublicStatusVersion: incident.PublicStatusVersion,
                RegulatorUserId: latestDecision.RegulatorUserId,
                Changed: false);
        }

        if (incident.RegulationStatus != RegulationStatus.UNDER_REVIEW)
        {
            throw new InvalidOperationException("Incident must be under review to receive a decision.");
        }

        var utcNow = DateTime.UtcNow;

        var decision = new RegulationDecision(
            incidentReferenceId: incident.Id,
            regulatorUserId: regulatorUserId,
            decisionType: decisionType,
            institutionType: institutionType,
            justification: normalizedJustification,
            utcNow: utcNow);

        var publicStatusChanged = false;

        switch (decisionType)
        {
            case RegulationDecisionType.DISPATCH_APPROVED:
                incident.AwaitDispatch(utcNow);
                publicStatusChanged = true;
                break;

            case RegulationDecisionType.NO_DISPATCH_REQUIRED:
                incident.Close(utcNow);
                publicStatusChanged = true;
                break;

            case RegulationDecisionType.MORE_INFORMATION_REQUIRED:
                incident.RequestMoreInformation(utcNow);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(decisionType));
        }

        await _decisionRepository.AddAsync(
            decision,
            cancellationToken);

        if (publicStatusChanged)
        {
            incident.AdvancePublicStatusVersion(utcNow);

            var eventId = $"evt-regulation-{javaIncidentId}-{incident.PublicStatusVersion}";

            var payload = JsonSerializer.Serialize(new
            {
                eventType = StatusChangedEventType,
                eventId,
                incidentId = javaIncidentId,
                status = incident.RegulationStatus.ToString(),
                version = incident.PublicStatusVersion,
                changedAt = utcNow
            });

            await _outbox.EnqueueAsync(
                eventId: eventId,
                eventType: StatusChangedEventType,
                routingKey: StatusChangedEventType,
                payload: payload,
                cancellationToken: cancellationToken);
        }

        await _incidentRepository.SaveChangesAsync(cancellationToken);

        return new RegulationDecisionResult(
            DecisionId: decision.Id,
            JavaIncidentId: incident.JavaIncidentId,
            DecisionType: decision.DecisionType,
            InstitutionType: decision.InstitutionType,
            RegulationStatus: incident.RegulationStatus,
            PublicStatusVersion: incident.PublicStatusVersion,
            RegulatorUserId: decision.RegulatorUserId,
            Changed: true);
    }
}
