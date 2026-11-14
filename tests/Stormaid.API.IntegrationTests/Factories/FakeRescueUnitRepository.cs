using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;

namespace Stormaid.API.IntegrationTests.Factories;

/// <summary>
/// Repositório em memória utilizado para isolar
/// os testes JWT da conexão com o Oracle.
/// </summary>
public sealed class FakeRescueUnitRepository : IRescueUnitRepository
{
    public Task<RescueUnit?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<RescueUnit?>(null);
    }

    public Task<IReadOnlyList<RescueUnit>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RescueUnit> units =
            Array.Empty<RescueUnit>();

        return Task.FromResult(units);
    }

    public Task<int> CountAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}