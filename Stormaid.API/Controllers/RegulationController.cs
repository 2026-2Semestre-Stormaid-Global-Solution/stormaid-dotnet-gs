using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;

namespace Stormaid.API.Controllers;

/// <summary>
/// Configuração das rotas para regulação
/// das ocorrências recebidas da API Java.
/// </summary>
public static class RegulationController
{
    /// <summary>
    /// Registra os endpoints de consulta e regulação
    /// das ocorrências recebidas pelo Storm Aid.
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
        var group = app.MapGroup("/api/v1/regulation/incidents")
            .WithTags("Regulação de Ocorrências")
            .RequireAuthorization("InstitutionalAccess")
            .RequireAuthorization(policy =>
                policy.RequireRole("REGULATOR"))
            .RequireRateLimiting("InstitutionalApi");

        group.MapGet("/{javaIncidentId:long}", GetByJavaIncidentIdAsync)
            .WithName("GetRegulationIncidentByJavaId")
            .WithSummary("Consultar ocorrência recebida do Java")
            .WithDescription(
                "Consulta os dados operacionais de uma ocorrência " +
                "recebida da API Java para regulação institucional.")
            .Produces<IncidentReferenceResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);

        return app;
    }

    /// <summary>
    /// Consulta uma ocorrência pelo identificador original
    /// atribuído pela API Java.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador original da ocorrência na API Java.
    /// </param>
    /// <param name="service">
    /// Serviço responsável pela consulta das referências
    /// de ocorrências armazenadas no Oracle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição.
    /// </param>
    /// <returns>
    /// Dados operacionais da ocorrência solicitada.
    /// </returns>
    /// <response code="200">
    /// Ocorrência encontrada e retornada com sucesso.
    /// </response>
    /// <response code="400">
    /// Identificador da ocorrência inválido.
    /// </response>
    /// <response code="401">
    /// Requisição não autenticada ou token JWT inválido.
    /// </response>
    /// <response code="403">
    /// Usuário autenticado sem autorização para
    /// consultar ocorrências institucionais.
    /// </response>
    /// <response code="404">
    /// Nenhuma ocorrência encontrada para o identificador informado.
    /// </response>
    /// <response code="429">
    /// Limite de requisições institucionais excedido.
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
                detail: "O identificador deve ser maior que zero.",
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
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Results.Ok(incident);
    }
}
