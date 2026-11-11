
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class UnitAssignment : Entity
{
    public long RescueUnitId { get; private set; }

    public long ResponderUserId { get; private set; }

    public long AssignedByRegulatorUserId { get; private set; }

    public UnitAssignmentStatus Status { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? EndedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public long Version { get; private set; }

    private UnitAssignment()
    {
    }

    public UnitAssignment(
        long rescueUnitId,
        long responderUserId,
        long assignedByRegulatorUserId,
        DateTime utcNow)
    {
        ValidateId(rescueUnitId, nameof(rescueUnitId));
        ValidateId(responderUserId, nameof(responderUserId));
        ValidateId(
            assignedByRegulatorUserId,
            nameof(assignedByRegulatorUserId)
        );

        ValidateUtc(utcNow);

        RescueUnitId = rescueUnitId;
        ResponderUserId = responderUserId;
        AssignedByRegulatorUserId = assignedByRegulatorUserId;

        Status = UnitAssignmentStatus.ACTIVE;

        StartedAt = utcNow;
        CreatedAt = utcNow;
        EndedAt = null;

        Version = 0;
    }

    public void End(DateTime utcNow)
    {
        if (Status == UnitAssignmentStatus.ENDED)
        {
            return;
        }

        ValidateUtc(utcNow);

        if (utcNow < StartedAt)
        {
            throw new ArgumentException(
                "End time cannot be earlier than start time.",
                nameof(utcNow)
            );
        }

        Status = UnitAssignmentStatus.ENDED;
        EndedAt = utcNow;

        Version = checked(Version + 1);
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

    private static void ValidateUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Date must be in UTC.",
                nameof(dateTime)
            );
        }
    }
}
