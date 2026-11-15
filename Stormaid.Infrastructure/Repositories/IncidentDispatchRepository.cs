
using Microsoft.EntityFrameworkCore;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Repositories;

public sealed class IncidentDispatchRepository : IIncidentDispatchRepository
{
    private readonly StormaidDbContext _context;

    public IncidentDispatchRepository(StormaidDbContext context)
    {
        _context = context
            ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<bool> HasActiveAssignmentAsync(
        long rescueUnitId,
        long responderUserId,
        CancellationToken cancellationToken = default)
    {
        if (rescueUnitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rescueUnitId));
        }

        if (responderUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responderUserId));
        }

        return await _context.UnitAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                    assignment.RescueUnitId == rescueUnitId &&
                    assignment.ResponderUserId == responderUserId &&
                    assignment.Status == UnitAssignmentStatus.ACTIVE,
                cancellationToken);
    }

    public async Task<IncidentDispatch?> GetTrackedByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return await _context.IncidentDispatches
            .AsTracking()
            .FirstOrDefaultAsync(
                dispatch => dispatch.Id == id,
                cancellationToken);
    }

    public async Task<IncidentDispatch?> GetActiveByIncidentReferenceIdAsync(
        long incidentReferenceId,
        CancellationToken cancellationToken = default)
    {
        if (incidentReferenceId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(incidentReferenceId));
        }

        return await _context.IncidentDispatches
            .AsNoTracking()
            .Where(dispatch =>
                dispatch.IncidentReferenceId == incidentReferenceId &&
                (
                    dispatch.Status == DispatchStatus.PENDING_ACCEPTANCE ||
                    dispatch.Status == DispatchStatus.ACCEPTED ||
                    dispatch.Status == DispatchStatus.EN_ROUTE ||
                    dispatch.Status == DispatchStatus.ON_SITE
                ))
            .OrderByDescending(dispatch => dispatch.DispatchedAt)
            .ThenByDescending(dispatch => dispatch.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(
        IncidentDispatch dispatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        await _context.IncidentDispatches.AddAsync(
            dispatch,
            cancellationToken);
    }

    public async Task AddHistoryAsync(
        DispatchStatusHistory history,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        await _context.DispatchStatusHistories.AddAsync(
            history,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
