
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.DTOs.Response;

public sealed record RegulationIncidentLocationResponse(
    decimal? Latitude,
    decimal? Longitude,
    string? AddressText
);

public sealed record RegulationIncidentItemResponse(
    long Id,
    long JavaIncidentId,
    RecipientType RecipientType,
    ChannelType ChannelType,
    EmergencyCategory EmergencyCategory,
    RegulationStatus RegulationStatus,
    RegulationIncidentLocationResponse Location,
    DateTime ReceivedAt
)
{
    public static RegulationIncidentItemResponse FromEntity(
        IncidentReference entity)
    {
        return new RegulationIncidentItemResponse(
            entity.Id,
            entity.JavaIncidentId,
            entity.RecipientType,
            entity.ChannelType,
            entity.EmergencyCategory,
            entity.RegulationStatus,
            new RegulationIncidentLocationResponse(
                entity.Latitude,
                entity.Longitude,
                entity.AddressText
            ),
            entity.ReceivedAt
        );
    }
}

public sealed record RegulationPageResponse(
    int Number,
    int Size,
    int TotalElements,
    int TotalPages
);


public sealed record RegulationIncidentsResponse(
    IReadOnlyList<RegulationIncidentItemResponse> Content,
    RegulationPageResponse Page
);
