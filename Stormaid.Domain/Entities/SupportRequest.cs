
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class SupportRequest : Entity
{
    public long IncidentReferenceId { get; private set; }

    public long IncidentDispatchId { get; private set; }

    public long RequestedByResponderUserId { get; private set; }

    public ResponderInstitutionType? RequestedInstitutionType
    {
        get;
        private set;
    }

    public string Reason { get; private set; } = null!;

    public SupportRequestStatus Status { get; private set; }

    public long? ReviewedByRegulatorUserId { get; private set; }

    public string? ReviewJustification { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReviewedAt { get; private set; }

    private SupportRequest()
    {
    }

    public SupportRequest(
        long incidentReferenceId,
        long incidentDispatchId,
        long requestedByResponderUserId,
        ResponderInstitutionType? requestedInstitutionType,
        string reason,
        DateTime utcNow)
    {
        ValidateId(incidentReferenceId, nameof(incidentReferenceId));
        ValidateId(incidentDispatchId, nameof(incidentDispatchId));
        ValidateId(requestedByResponderUserId, nameof(requestedByResponderUserId));

        if (requestedInstitutionType.HasValue &&
            !Enum.IsDefined(requestedInstitutionType.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedInstitutionType),
                "Invalid institution type."
            );
        }

        var validatedReason = ValidateText(reason, nameof(reason));

        ValidateUtc(utcNow);

        IncidentReferenceId = incidentReferenceId;
        IncidentDispatchId = incidentDispatchId;
        RequestedByResponderUserId = requestedByResponderUserId;

        RequestedInstitutionType = requestedInstitutionType;
        Reason = validatedReason;

        Status = SupportRequestStatus.OPEN;

        ReviewedByRegulatorUserId = null;
        ReviewJustification = null;

        CreatedAt = utcNow;
        ReviewedAt = null;
    }

    public void Approve(
        long regulatorUserId,
        string justification,
        DateTime utcNow)
    {
        Review(
            SupportRequestStatus.APPROVED,
            regulatorUserId,
            justification,
            utcNow
        );
    }

    public void Decline(
        long regulatorUserId,
        string justification,
        DateTime utcNow)
    {
        Review(
            SupportRequestStatus.DECLINED,
            regulatorUserId,
            justification,
            utcNow
        );
    }

    private void Review(
        SupportRequestStatus nextStatus,
        long regulatorUserId,
        string justification,
        DateTime utcNow)
    {
        if (Status != SupportRequestStatus.OPEN)
        {
            throw new InvalidOperationException("Only open support requests can be reviewed.");
        }

        ValidateId(regulatorUserId,nameof(regulatorUserId));

        var validatedJustification = ValidateText(justification,nameof(justification));

        ValidateUtc(utcNow);

        if (utcNow < CreatedAt)
        {
            throw new ArgumentException(
                "Review date cannot be earlier than creation date.",
                nameof(utcNow)
            );
        }

        Status = nextStatus;
        ReviewedByRegulatorUserId = regulatorUserId;
        ReviewJustification = validatedJustification;
        ReviewedAt = utcNow;
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

    private static string ValidateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Text is required.",
                parameterName
            );
        }

        var normalized = value.Trim();

        if (normalized.Length is < 10 or > 1000)
        {
            throw new ArgumentException(
                "Text must contain between 10 and 1000 characters.",
                parameterName
            );
        }

        return normalized;
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
