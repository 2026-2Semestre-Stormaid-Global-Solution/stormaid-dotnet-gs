namespace Stormaid.Application.DTOs.Request;

/// <summary>
/// Representa os dados necessários para solicitar
/// a criação de um despacho operacional.
/// </summary>
public sealed record CreateIncidentDispatchRequest(
    long JavaIncidentId,
    long RescueUnitId,
    string? Notes
);