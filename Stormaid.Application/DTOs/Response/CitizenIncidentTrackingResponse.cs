namespace Stormaid.Application.DTOs.Response;

/// <summary>
/// Dados de acompanhamento institucional
/// disponibilizados ao cidadão.
/// </summary>
public sealed record CitizenIncidentTrackingResponse(
    long JavaIncidentId,
    string RegulationStatus,
    long PublicStatusVersion
);