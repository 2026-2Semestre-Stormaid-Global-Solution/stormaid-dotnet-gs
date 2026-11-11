
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class IncidentDispatch : Entity
{
    public long IncidentReferenceId { get; private set; }

    public long RegulationDecisionId { get; private set; }

    public long RescueUnitId { get; private set; }

    public DispatchStatus Status { get; private set; }

    public long? AcceptedByResponderUserId { get; private set; }

    public long CreatedByRegulatorUserId { get; private set; }

    public string? Notes { get; private set; }

    public DateTime DispatchedAt { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    public DateTime? EnRouteAt { get; private set; }

    public DateTime? ArrivedAt { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public long Version { get; private set; }

    private IncidentDispatch()
    {
    }

    public IncidentDispatch(
        long incidentReferenceId,
        long regulationDecisionId,
        long rescueUnitId,
        long createdByRegulatorUserId,
        string? notes,
        DateTime utcNow)
    {
        ValidateId(incidentReferenceId, nameof(incidentReferenceId));

        ValidateId(regulationDecisionId, nameof(regulationDecisionId));

        ValidateId(rescueUnitId, nameof(rescueUnitId));

        ValidateId(createdByRegulatorUserId, nameof(createdByRegulatorUserId));

        ValidateUtc(utcNow);

        IncidentReferenceId = incidentReferenceId;
        RegulationDecisionId = regulationDecisionId;
        RescueUnitId = rescueUnitId;
        CreatedByRegulatorUserId = createdByRegulatorUserId;

        Notes = NormalizeNotes(notes);

        Status = DispatchStatus.PENDING_ACCEPTANCE;
        DispatchedAt = utcNow;

        Version = 0;
    }

    public void Accept(long responderUserId, DateTime utcNow)
    {
        ValidateId(responderUserId, nameof(responderUserId));

        if (Status == DispatchStatus.ACCEPTED)
        {
            if (AcceptedByResponderUserId == responderUserId) return;

            throw new InvalidOperationException(
                "Dispatch was accepted by another responder."
            );
        }

        EnsureStatus(DispatchStatus.PENDING_ACCEPTANCE);
        ValidateTransitionTime(utcNow);

        var nextVersion = checked(Version + 1);

        AcceptedByResponderUserId = responderUserId;
        AcceptedAt = utcNow;
        Status = DispatchStatus.ACCEPTED;

        Version = nextVersion;
    }

    public void StartRoute(DateTime utcNow)
    {
        if (Status == DispatchStatus.EN_ROUTE) return;

        EnsureStatus(DispatchStatus.ACCEPTED);
        ValidateTransitionTime(utcNow);

        var nextVersion = checked(Version + 1);

        EnRouteAt = utcNow;
        Status = DispatchStatus.EN_ROUTE;

        Version = nextVersion;
    }

    public void Arrive(DateTime utcNow)
    {
        if (Status == DispatchStatus.ON_SITE) return;

        EnsureStatus(DispatchStatus.EN_ROUTE);
        ValidateTransitionTime(utcNow);

        var nextVersion = checked(Version + 1);

        ArrivedAt = utcNow;
        Status = DispatchStatus.ON_SITE;

        Version = nextVersion;
    }

    public void Resolve(DateTime utcNow)
    {
        if (Status == DispatchStatus.RESOLVED) return;

        EnsureStatus(DispatchStatus.ON_SITE);
        ValidateTransitionTime(utcNow);

        var nextVersion = checked(Version + 1);

        ResolvedAt = utcNow;
        Status = DispatchStatus.RESOLVED;

        Version = nextVersion;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status == DispatchStatus.CANCELLED) return;

        if (Status == DispatchStatus.RESOLVED)
        {
            throw new InvalidOperationException(
                "Resolved dispatches cannot be cancelled."
            );
        }

        ValidateTransitionTime(utcNow);

        var nextVersion = checked(Version + 1);

        CancelledAt = utcNow;
        Status = DispatchStatus.CANCELLED;

        Version = nextVersion;
    }

    private void EnsureStatus(DispatchStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Expected status {expected}, but current status is {Status}."
            );
        }
    }

    private void ValidateTransitionTime(DateTime utcNow)
    {
        ValidateUtc(utcNow);

        var previousTimestamp = Status switch
        {
            DispatchStatus.PENDING_ACCEPTANCE => DispatchedAt,
            DispatchStatus.ACCEPTED => AcceptedAt!.Value,
            DispatchStatus.EN_ROUTE => EnRouteAt!.Value,
            DispatchStatus.ON_SITE => ArrivedAt!.Value,
            DispatchStatus.RESOLVED => ResolvedAt!.Value,
            DispatchStatus.CANCELLED => CancelledAt!.Value,

            _ => throw new InvalidOperationException("Invalid dispatch status.")
        };

        if (utcNow < previousTimestamp)
        {
            throw new ArgumentException(
                "Transition time cannot be earlier than the previous transition.",
                nameof(utcNow)
            );
        }
    }

    private static void ValidateId(long id, string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Identifier must be greater than zero."
            );
        }
    }

    private static void ValidateUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Date must be in UTC.",
                nameof(utcNow)
            );
        }
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var normalized = notes.Trim();

        if (normalized.Length > 1000)
        {
            throw new ArgumentException(
                "Notes must contain at most 1000 characters.",
                nameof(notes)
            );
        }

        return normalized;
    }
}
