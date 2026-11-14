
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;
using Stormaid.Domain.Enums;

namespace Stormaid.API.Controllers;

/// <summary>
/// Configura as rotas institucionais de consulta e
/// regulação das ocorrências recebidas da API Java.
/// </summary>
public static class RegulationController
{
    /// <summary>
    /// Registra os endpoints de consulta das ocorrências
    /// disponíveis para os reguladores institucionais.
    /// </summary>
    /// <param name="app">
    /// Construtor de rotas da aplicação.
    /// </param>
    /// <returns>
    /// Construtor de rotas com os endpoints registrados.
    /// </returns>
    public static IEndpointRouteBuilder MapRegulationRoutes(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
                "/api/v1/regulation/incidents")
            .WithTags("Regulação de Ocorrências")
            .RequireAuthorization("InstitutionalAccess")
            .RequireAuthorization(
                policy => policy.RequireRole("REGULATOR"))
            .RequireRateLimiting("InstitutionalApi");

        group.MapGet("", GetPagedAsync)
            .WithName("GetRegulationIncidents")
            .WithSummary("Listar ocorrências da regulação")
            .WithDescription(
                "Consulta a fila de ocorrências com filtro " +
                "opcional de status e paginação iniciada em zero.")
            .Produces<RegulationIncidentsResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);

        group.MapGet(
                "/{javaIncidentId:long}",
                GetByJavaIncidentIdAsync)
            .WithName("GetRegulationIncidentByJavaId")
            .WithSummary(
                "Consultar ocorrência pelo identificador Java")
            .WithDescription(
                "Busca a referência operacional de uma " +
                "ocorrência recebida da API Java.")
            .Produces<IncidentReferenceResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);

        return app;
    }

    /// <summary>
    /// Lista as ocorrências disponíveis na fila de regulação.
    /// Permite filtrar pelo status e consultar os resultados
    /// de maneira paginada.
    /// </summary>
    /// <param name="service">
    /// Serviço de consulta das referências de ocorrências.
    /// </param>
    /// <param name="status">
    /// Status de regulação utilizado como filtro.
    /// Quando não informado, considera todos os status.
    /// </param>
    /// <param name="page">
    /// Número da página, iniciando em zero.
    /// Quando omitido, utiliza zero.
    /// </param>
    /// <param name="size">
    /// Quantidade de registros por página, entre 1 e 100.
    /// Quando omitida, utiliza 10.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição HTTP.
    /// </param>
    /// <returns>
    /// Resposta paginada contendo as ocorrências
    /// e os metadados da paginação.
    /// </returns>
    /// <response code="200">
    /// Consulta realizada com sucesso.
    /// </response>
    /// <response code="400">
    /// Status ou parâmetros de paginação inválidos.
    /// </response>
    /// <response code="401">
    /// Token JWT ausente ou inválido.
    /// </response>
    /// <response code="403">
    /// Usuário sem permissão institucional de regulador.
    /// </response>
    /// <response code="429">
    /// Limite de requisições excedido.
    /// </response>
    private static async Task<IResult> GetPagedAsync(
        IncidentReferenceQueryService service,
        RegulationStatus? status,
        int? page,
        int? size,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetPagedAsync(
                status: status,
                page: page ?? 0,
                pageSize: size ?? 10,
                cancellationToken: cancellationToken
            );

            return Results.Ok(result);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.Problem(
                title: "Parâmetros de consulta inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    }

    /// <summary>
    /// Consulta uma ocorrência pelo identificador original
    /// utilizado na API Java.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador da ocorrência no serviço Java.
    /// Não corresponde à chave primária interna do .NET.
    /// </param>
    /// <param name="service">
    /// Serviço de consulta das referências de ocorrências.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição HTTP.
    /// </param>
    /// <returns>
    /// Dados da ocorrência encontrada ou uma resposta
    /// de erro correspondente.
    /// </returns>
    /// <response code="200">
    /// Ocorrência encontrada.
    /// </response>
    /// <response code="400">
    /// Identificador Java inválido.
    /// </response>
    /// <response code="401">
    /// Token JWT ausente ou inválido.
    /// </response>
    /// <response code="403">
    /// Usuário sem permissão institucional de regulador.
    /// </response>
    /// <response code="404">
    /// Ocorrência não encontrada.
    /// </response>
    /// <response code="429">
    /// Limite de requisições excedido.
    /// </response>
    private static async Task<IResult> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        IncidentReferenceQueryService service,
        CancellationToken cancellationToken)
    {
        if (javaIncidentId <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O identificador Java deve ser maior que zero.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var incident = await service.GetByJavaIncidentIdAsync(
            javaIncidentId,
            cancellationToken
        );

        if (incident is null)
        {
            return Results.Problem(
                title: "Ocorrência não encontrada.",
                detail:
                    $"Não existe uma ocorrência com o " +
                    $"identificador Java {javaIncidentId}.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Results.Ok(incident);
    }
}
