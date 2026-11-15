namespace Stormaid.Application.DTOs.Response;

public sealed record RegulationDecisionResponse(
    long DecisionId,
    long JavaIncidentId,
    string DecisionType,
    string? InstitutionType,
    string RegulationStatus,
    long PublicStatusVersion,
    long RegulatorUserId,
    bool Changed
);
