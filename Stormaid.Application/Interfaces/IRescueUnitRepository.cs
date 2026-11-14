using Stormaid.Domain.Entities;

namespace Stormaid.Application.Interfaces;

public interface IRescueUnitRepository
{
    Task<RescueUnit?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RescueUnit>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task<int> CountAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsByUnitCodeAsync(
        string unitCode,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        RescueUnit unit,
        CancellationToken cancellationToken = default
    );
}