using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.DTOs.Response;

/// <summary>
/// Representa os dados de um despacho operacional
/// retornados pela API Storm Aid.
/// </summary>
public sealed record IncidentDispatchResponse(
    long Id,
    long IncidentReferenceId,
    long RegulationDecisionId,
    long RescueUnitId,
    DispatchStatus Status,
    long CreatedByRegulatorUserId,
    string? Notes,
    DateTime DispatchedAt,
    long Version
)
{
    public static IncidentDispatchResponse FromEntity(
        IncidentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        return new IncidentDispatchResponse(
            Id: dispatch.Id,
            IncidentReferenceId: dispatch.IncidentReferenceId,
            RegulationDecisionId: dispatch.RegulationDecisionId,
            RescueUnitId: dispatch.RescueUnitId,
            Status: dispatch.Status,
            CreatedByRegulatorUserId: dispatch.CreatedByRegulatorUserId,
            Notes: dispatch.Notes,
            DispatchedAt: dispatch.DispatchedAt,
            Version: dispatch.Version
        );
    }
}