
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class RegulationDecision : Entity
{
    public long IncidentReferenceId { get; private set; }

    public long RegulatorUserId { get; private set; }

    public RegulationDecisionType DecisionType { get; private set; }

    public ResponderInstitutionType? InstitutionType { get; private set; }

    public string Justification { get; private set; } = null!;

    public DateTime DecidedAt { get; private set; }

    private RegulationDecision()
    {
    }

    public RegulationDecision(
        long incidentReferenceId,
        long regulatorUserId,
        RegulationDecisionType decisionType,
        ResponderInstitutionType? institutionType,
        string justification,
        DateTime utcNow)
    {
        ValidateId(
            incidentReferenceId,
            nameof(incidentReferenceId)
        );

        ValidateId(
            regulatorUserId,
            nameof(regulatorUserId)
        );

        ValidateDecisionType(decisionType);

        ValidateInstitutionType(
            decisionType,
            institutionType
        );

        var validatedJustification =
            ValidateJustification(justification);

        ValidateUtc(utcNow);

        IncidentReferenceId = incidentReferenceId;
        RegulatorUserId = regulatorUserId;

        DecisionType = decisionType;
        InstitutionType = institutionType;

        Justification = validatedJustification;
        DecidedAt = utcNow;
    }

    private static void ValidateId(
        long id,
        string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Identifier must be greater than zero."
            );
        }
    }

    private static void ValidateDecisionType(
        RegulationDecisionType decisionType)
    {
        if (!Enum.IsDefined(decisionType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(decisionType),
                "Invalid regulation decision type."
            );
        }
    }

    private static void ValidateInstitutionType(
        RegulationDecisionType decisionType,
        ResponderInstitutionType? institutionType)
    {
        if (decisionType == RegulationDecisionType.DISPATCH_APPROVED)
        {
            if (!institutionType.HasValue)
            {
                throw new ArgumentException(
                    "An approved dispatch requires an institution type.",
                    nameof(institutionType)
                );
            }

            if (!Enum.IsDefined(institutionType.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(institutionType),
                    "Invalid responder institution type."
                );
            }

            return;
        }

        if (institutionType.HasValue)
        {
            throw new ArgumentException(
                "Institution type must be null when dispatch is not approved.",
                nameof(institutionType)
            );
        }
    }

    private static string ValidateJustification(
        string justification)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new ArgumentException(
                "Justification is required.",
                nameof(justification)
            );
        }

        var normalizedJustification = justification.Trim();

        if (normalizedJustification.Length is < 10 or > 1000)
        {
            throw new ArgumentException(
                "Justification must contain between 10 and 1000 characters.",
                nameof(justification)
            );
        }

        return normalizedJustification;
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
}
