
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class DispatchStatusHistory : Entity
{
    public long IncidentDispatchId { get; private set; }

    public DispatchStatus? PreviousStatus { get; private set; }

    public DispatchStatus NewStatus { get; private set; }

    public long ChangedByUserId { get; private set; }

    public DispatchActorRole ChangedByRole { get; private set; }

    public string? Note { get; private set; }

    public DateTime ChangedAt { get; private set; }

    private DispatchStatusHistory() {}

    public DispatchStatusHistory(
        long incidentDispatchId,
        DispatchStatus? previousStatus,
        DispatchStatus newStatus,
        long changedByUserId,
        DispatchActorRole changedByRole,
        string? note,
        DateTime utcNow)
    {
        ValidateId(incidentDispatchId, nameof(incidentDispatchId));

        ValidateId(changedByUserId, nameof(changedByUserId));

        if (previousStatus.HasValue &&
            !Enum.IsDefined(previousStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousStatus),
                "Invalid previous dispatch status."
            );
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                "Invalid new dispatch status."
            );
        }

        if (!Enum.IsDefined(changedByRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(changedByRole),
                "Invalid actor role."
            );
        }

        ValidateUtc(utcNow);

        ValidateTransition(
            previousStatus,
            newStatus,
            changedByRole
        );

        IncidentDispatchId = incidentDispatchId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        ChangedByRole = changedByRole;

        Note = NormalizeNote(note);
        ChangedAt = utcNow;
    }

    private static void ValidateTransition(
        DispatchStatus? previousStatus,
        DispatchStatus newStatus,
        DispatchActorRole changedByRole)
    {
        var allowed = (
            previousStatus,
            newStatus,
            changedByRole
        ) switch
        {
            (
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                DispatchActorRole.REGULATOR
            ) => true,

            (
                DispatchStatus.PENDING_ACCEPTANCE,
                DispatchStatus.ACCEPTED,
                DispatchActorRole.RESPONDER
            ) => true,

            (
                DispatchStatus.ACCEPTED,
                DispatchStatus.EN_ROUTE,
                DispatchActorRole.RESPONDER
            ) => true,

            (
                DispatchStatus.EN_ROUTE,
                DispatchStatus.ON_SITE,
                DispatchActorRole.RESPONDER
            ) => true,

            (
                DispatchStatus.ON_SITE,
                DispatchStatus.RESOLVED,
                DispatchActorRole.RESPONDER
            ) => true,

            (
                DispatchStatus.PENDING_ACCEPTANCE or
                DispatchStatus.ACCEPTED or
                DispatchStatus.EN_ROUTE or
                DispatchStatus.ON_SITE,
                DispatchStatus.CANCELLED,
                DispatchActorRole.REGULATOR
            ) => true,

            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException("Invalid dispatch transition or actor role.");
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
            throw new ArgumentException("Date must be in UTC.", nameof(utcNow));
        }
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var normalized = note.Trim();

        if (normalized.Length > 500)
        {
            throw new ArgumentException(
                "Note must contain at most 500 characters.",
                nameof(note)
            );
        }

        return normalized;
    }
}
