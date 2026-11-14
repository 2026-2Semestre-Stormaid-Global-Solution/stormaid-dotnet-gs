using Stormaid.Domain.Enums;

namespace Stormaid.Application.DTOs.Integration;

/// <summary>
/// Representa o evento de ocorrência criada
/// publicado pela API Java.
/// </summary>
public sealed record IncidentCreatedEvent(
    long? IncidentId,
    long? RequesterUserId,
    RecipientType RecipientType,
    ChannelType ChannelType,
    EmergencyCategory EmergencyCategory,
    string? PublicStatus,
    IncidentEventLocation? Location,
    DateTimeOffset? CreatedAt
);

/// <summary>
/// Representa os dados de localização
/// recebidos no evento do Java.
/// </summary>
public sealed record IncidentEventLocation(
    string? Source,
    decimal? Latitude,
    decimal? Longitude,
    string? AddressText,
    string? ReferencePoint,
    decimal? AccuracyMeters,
    DateTimeOffset? LocationCapturedAt
);