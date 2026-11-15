using Microsoft.EntityFrameworkCore;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Repositories;

public class RescueUnitRepository : IRescueUnitRepository
{
    private readonly StormaidDbContext _context;

    public RescueUnitRepository(StormaidDbContext context)
    {
        _context = context;
    }

    public async Task<RescueUnit?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RescueUnit>().AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken
            );
    }
    
    
    public async Task<RescueUnit?> GetTrackedByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return await _context.Set<RescueUnit>()
            .AsTracking()
            .FirstOrDefaultAsync(
                unit => unit.Id == id,
                cancellationToken);
    }


    public async Task<IReadOnlyList<RescueUnit>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));

        if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var offset = (long)(page - 1) * pageSize;

        if (offset > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(page));

        return await _context.Set<RescueUnit>()
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<RescueUnit>().AsNoTracking().CountAsync(cancellationToken);
    }
    
    public async Task<bool> ExistsByUnitCodeAsync(
        string unitCode,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<RescueUnit>()
            .AsNoTracking()
            .AnyAsync(
                unit => unit.UnitCode == unitCode,
                cancellationToken
            );
    }
    
    public async Task AddAsync(
        RescueUnit unit,
        CancellationToken cancellationToken = default)
    {
        await _context.Set<RescueUnit>()
            .AddAsync(unit, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}