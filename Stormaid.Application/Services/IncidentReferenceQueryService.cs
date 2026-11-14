
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Enums;

namespace Stormaid.Application.Services;

/// <summary>
/// Serviço responsável pelas consultas de ocorrências
/// recebidas da API Java e persistidas no Oracle.
/// </summary>
public sealed class IncidentReferenceQueryService
{
    private readonly IIncidentReferenceRepository _repository;

    /// <summary>
    /// Inicializa o serviço de consulta de ocorrências.
    /// </summary>
    /// <param name="repository">
    /// Repositório responsável pelo acesso às ocorrências.
    /// </param>
    public IncidentReferenceQueryService(
        IIncidentReferenceRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Consulta uma ocorrência pelo identificador original da API Java.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador da ocorrência na API Java.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Dados da ocorrência encontrada ou null quando inexistente.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Lançada quando o identificador é menor ou igual a zero.
    /// </exception>
    public async Task<IncidentReferenceResponse?> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        CancellationToken cancellationToken = default)
    {
        if (javaIncidentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(javaIncidentId)
            );
        }

        var incident = await _repository.GetByJavaIncidentIdAsync(
            javaIncidentId,
            cancellationToken
        );

        return incident is null
            ? null
            : IncidentReferenceResponse.FromEntity(incident);
    }

    /// <summary>
    /// Consulta a fila de ocorrências da regulação
    /// com paginação e filtro opcional de status.
    /// </summary>
    /// <param name="status">
    /// Status de regulação utilizado como filtro.
    /// Quando nulo, retorna ocorrências de todos os status.
    /// </param>
    /// <param name="page">
    /// Número da página, começando em zero.
    /// </param>
    /// <param name="pageSize">
    /// Quantidade de registros por página, entre 1 e 100.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Ocorrências encontradas e metadados da paginação.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Lançada quando os parâmetros de consulta são inválidos.
    /// </exception>
    public async Task<RegulationIncidentsResponse> GetPagedAsync(
        RegulationStatus? status,
        int page = 0,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if ((long)page * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        var totalElements = await _repository.CountAsync(
            status,
            cancellationToken
        );

        var incidents = await _repository.GetPagedAsync(
            status,
            page,
            pageSize,
            cancellationToken
        );

        var content = incidents
            .Select(RegulationIncidentItemResponse.FromEntity)
            .ToList();

        var totalPages = (int)(
            ((long)totalElements + pageSize - 1) / pageSize
        );

        var pagination = new RegulationPageResponse(
            Number: page,
            Size: pageSize,
            TotalElements: totalElements,
            TotalPages: totalPages
        );

        return new RegulationIncidentsResponse(
            Content: content,
            Page: pagination
        );
    }
}
