using Stormaid.Domain.Entities;

namespace Stormaid.Application.Interfaces;

public interface IIncidentDispatchRepository
{
    Task<IncidentDispatch?> GetTrackedByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    );

    Task<IncidentDispatch?> GetActiveByIncidentReferenceIdAsync(
        long incidentReferenceId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        IncidentDispatch dispatch,
        CancellationToken cancellationToken = default
    );

    Task AddHistoryAsync(
        DispatchStatusHistory history,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default
    );
}