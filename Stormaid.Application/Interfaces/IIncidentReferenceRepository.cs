using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Interfaces;

/// <summary>
/// Define as operações de persistência
/// das ocorrências recebidas da API Java.
/// </summary>
public interface IIncidentReferenceRepository
{
    /// <summary>
    /// Busca uma ocorrência pelo identificador interno do .NET.
    /// </summary>
    Task<IncidentReference?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Busca uma ocorrência pelo identificador original do Java.
    /// </summary>
    Task<IncidentReference?> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Persiste uma nova referência de ocorrência.
    /// </summary>
    Task AddAsync(
        IncidentReference incidentReference,
        CancellationToken cancellationToken = default
    );


    /// <summary>
    /// Consulta as ocorrências com paginação e filtro
    /// opcional pelo status de regulação.
    /// </summary>
    /// <param name="status">
    /// Status de regulação utilizado como filtro.
    /// Quando nulo, consulta todos os status.
    /// </param>
    /// <param name="page">
    /// Número da página, iniciando em zero.
    /// </param>
    /// <param name="pageSize">
    /// Quantidade de registros por página.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da consulta.
    /// </param>
    /// <returns>
    /// Lista de ocorrências correspondente à página solicitada.
    /// </returns>
    Task<IReadOnlyList<IncidentReference>> GetPagedAsync(
        RegulationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Conta as ocorrências conforme o filtro de regulação.
    /// </summary>
    /// <param name="status">
    /// Status opcional utilizado na contagem.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da consulta.
    /// </param>
    /// <returns>
    /// Quantidade total de ocorrências que atendem ao filtro.
    /// </returns>
    Task<int> CountAsync(
        RegulationStatus? status,
        CancellationToken cancellationToken = default
    );

}