using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;

namespace Stormaid.Application.Services;

public sealed class RescueUnitQueryService
{
    private readonly IRescueUnitRepository _repository;

    public RescueUnitQueryService(IRescueUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<RescueUnitResponse?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        var entity = await _repository.GetByIdAsync(
            id,
            cancellationToken
        );

        return entity is null
            ? null
            : RescueUnitResponse.FromEntity(entity);
    }

    public async Task<PagedResponse<RescueUnitResponse>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var entities = await _repository.GetPagedAsync(
            page,
            pageSize,
            cancellationToken
        );

        var totalItems = await _repository.CountAsync(
            cancellationToken
        );

        var items = entities
            .Select(RescueUnitResponse.FromEntity)
            .ToList();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize
        );

        return new PagedResponse<RescueUnitResponse>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages
        );
    }
}