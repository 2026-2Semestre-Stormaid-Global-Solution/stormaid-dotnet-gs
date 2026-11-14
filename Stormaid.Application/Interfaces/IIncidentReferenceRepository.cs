using Stormaid.Domain.Entities;

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
}