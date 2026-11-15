namespace Stormaid.Application.DTOs.Request;

public sealed record CreateRegulationDecisionRequest(
    string? DecisionType,
    string? InstitutionType,
    string? Justification
);