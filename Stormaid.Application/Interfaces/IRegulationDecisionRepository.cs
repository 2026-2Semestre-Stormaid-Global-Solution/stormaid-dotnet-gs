using Stormaid.Domain.Entities;

namespace Stormaid.Application.Interfaces;

/// <summary>
/// Define as operações de persistência das decisões
/// regulatórias registradas pelos usuários institucionais.
/// </summary>
public interface IRegulationDecisionRepository
{
    /// <summary>
    /// Adiciona uma decisão regulatória ao contexto
    /// de persistência, sem executar SaveChanges.
    /// </summary>
    /// <param name="decision">
    /// Decisão regulatória a ser registrada.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Tarefa que representa a inclusão no contexto.
    /// </returns>
    Task AddAsync(
        RegulationDecision decision,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Consulta a decisão regulatória mais recente
    /// de uma ocorrência pelo identificador interno.
    /// </summary>
    /// <param name="incidentReferenceId">
    /// Identificador interno da ocorrência no Oracle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Decisão mais recente ou null quando inexistente.
    /// </returns>
    Task<RegulationDecision?> GetLatestByIncidentReferenceIdAsync(
        long incidentReferenceId,
        CancellationToken cancellationToken = default
    );
}