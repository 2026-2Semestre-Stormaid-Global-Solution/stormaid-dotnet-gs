using Microsoft.EntityFrameworkCore;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Repositories;

/// <summary>
/// Implementa a persistência das decisões regulatórias
/// utilizando o Entity Framework Core e Oracle 19c.
/// </summary>
public sealed class RegulationDecisionRepository
    : IRegulationDecisionRepository
{
    private readonly StormaidDbContext _context;

    /// <summary>
    /// Inicializa o repositório de decisões regulatórias.
    /// </summary>
    /// <param name="context">
    /// Contexto de persistência compartilhado da aplicação.
    /// </param>
    public RegulationDecisionRepository(
        StormaidDbContext context)
    {
        _context = context
            ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Adiciona uma decisão regulatória ao contexto
    /// sem persistir imediatamente no banco.
    /// </summary>
    /// <param name="decision">
    /// Decisão regulatória que será registrada.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    public async Task AddAsync(
        RegulationDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        await _context.RegulationDecisions.AddAsync(
            decision,
            cancellationToken
        );
    }

    /// <summary>
    /// Consulta a decisão regulatória mais recente
    /// de uma ocorrência pelo identificador interno.
    /// </summary>
    /// <param name="incidentReferenceId">
    /// Identificador interno da ocorrência no Oracle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da consulta.
    /// </param>
    /// <returns>
    /// Decisão mais recente ou null quando inexistente.
    /// </returns>
    public async Task<RegulationDecision?>
        GetLatestByIncidentReferenceIdAsync(
            long incidentReferenceId,
            CancellationToken cancellationToken = default)
    {
        if (incidentReferenceId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(incidentReferenceId),
                "O identificador da ocorrência deve ser maior que zero."
            );
        }

        return await _context.RegulationDecisions
            .AsNoTracking()
            .Where(decision =>
                decision.IncidentReferenceId == incidentReferenceId)
            .OrderByDescending(decision => decision.DecidedAt)
            .ThenByDescending(decision => decision.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
