namespace Stormaid.Application.DTOs.Request;

public sealed record CancelIncidentDispatchRequest(
    string? Reason
);