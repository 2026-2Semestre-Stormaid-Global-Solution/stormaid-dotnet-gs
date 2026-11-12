using Stormaid.Domain.Entities;

namespace Stormaid.Application.DTOs.Response;

public sealed record RescueUnitResponse(
    long Id,
    string UnitCode,
    string DisplayName,
    string InstitutionType,
    int Capacity,
    bool SupportsAccessibleTransport,
    string OperationalStatus,
    bool Active,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static RescueUnitResponse FromEntity(RescueUnit entity)
    {
        return new RescueUnitResponse(
            entity.Id,
            entity.UnitCode,
            entity.DisplayName,
            entity.InstitutionType.ToString(),
            entity.Capacity,
            entity.SupportsAccessibleTransport,
            entity.OperationalStatus.ToString(),
            entity.Active,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}