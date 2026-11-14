using Microsoft.EntityFrameworkCore;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Infrastructure.Data;
using Stormaid.Domain.Enums;

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


    /// <summary>
    /// Consulta as ocorrências persistidas no Oracle
    /// com paginação e filtro opcional de status.
    /// </summary>
    public async Task<IReadOnlyList<IncidentReference>> GetPagedAsync(
        RegulationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var offset = (long)page * pageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        var query = _context.IncidentReferences
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(
                incident => incident.RegulationStatus == status.Value
            );
        }

        return await query
            .OrderByDescending(incident => incident.ReceivedAt)
            .ThenByDescending(incident => incident.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retorna o total de ocorrências conforme
    /// o filtro opcional de status.
    /// </summary>
    public async Task<int> CountAsync(
        RegulationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IncidentReferences
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(
                incident => incident.RegulationStatus == status.Value
            );
        }

        return await query.CountAsync(cancellationToken);
    }


    /// <summary>
    /// Consulta uma ocorrência pelo identificador Java
    /// mantendo seu rastreamento para atualizações.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador da ocorrência na API Java.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da consulta.
    /// </param>
    /// <returns>
    /// Entidade rastreada ou null quando não encontrada.
    /// </returns>
    public async Task<IncidentReference?> GetTrackedByJavaIncidentIdAsync(
        long javaIncidentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.IncidentReferences
            .AsTracking()
            .FirstOrDefaultAsync(
                incident => incident.JavaIncidentId == javaIncidentId,
                cancellationToken
            );
    }

    /// <summary>
    /// Salva as alterações das entidades rastreadas
    /// no banco de dados Oracle.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Tarefa que representa a persistência.
    /// </returns>
    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }


}