using Microsoft.EntityFrameworkCore;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Repositories;

/// <summary>
/// Implementa a persistência das referências de ocorrências
/// recebidas da API Java utilizando Oracle.
/// </summary>
public sealed class IncidentReferenceRepository : IIncidentReferenceRepository
{
    private readonly StormaidDbContext _context;

    public IncidentReferenceRepository(StormaidDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Consulta uma ocorrência pelo identificador interno.
    /// </summary>
    public async Task<IncidentReference?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _context.IncidentReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                incident => incident.Id == id,
                cancellationToken
            );
    }

    /// <summary>
    /// Consulta uma ocorrência pelo identificador original do Java.
    /// </summary>
    public async Task<IncidentReference?> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.IncidentReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                incident => incident.JavaIncidentId == javaIncidentId,
                cancellationToken
            );
    }

    /// <summary>
    /// Persiste uma nova referência de ocorrência.
    /// </summary>
    public async Task AddAsync(
        IncidentReference incidentReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incidentReference);

        await _context.IncidentReferences.AddAsync(
            incidentReference,
            cancellationToken
        );

        await _context.SaveChangesAsync(cancellationToken);
    }
}