using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.DTOs.Response;

public sealed record IncidentReferenceResponse(
    long Id,
    long JavaIncidentId,
    long RequesterUserId,
    RecipientType RecipientType,
    ChannelType ChannelType,
    EmergencyCategory EmergencyCategory,
    decimal? Latitude,
    decimal? Longitude,
    string? AddressText,
    RegulationStatus RegulationStatus,
    long PublicStatusVersion,
    DateTime ReceivedAt,
    DateTime? JavaCreatedAt,
    DateTime UpdatedAt
)
{
    public static IncidentReferenceResponse FromEntity(
        IncidentReference entity)
    {
        return new IncidentReferenceResponse(
            entity.Id,
            entity.JavaIncidentId,
            entity.RequesterUserId,
            entity.RecipientType,
            entity.ChannelType,
            entity.EmergencyCategory,
            entity.Latitude,
            entity.Longitude,
            entity.AddressText,
            entity.RegulationStatus,
            entity.PublicStatusVersion,
            entity.ReceivedAt,
            entity.JavaCreatedAt,
            entity.UpdatedAt
        );
    }
}