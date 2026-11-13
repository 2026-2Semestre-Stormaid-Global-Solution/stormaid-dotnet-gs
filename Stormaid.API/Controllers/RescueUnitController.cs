
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;

namespace Stormaid.API.Controllers;
/// <summary>
/// Configuração das rotas para gerenciamento
/// das unidades de resgate do Storm Aid.
/// </summary>
public static class RescueUnitController
{
    /// <summary>
    /// Registra os endpoints de consulta das unidades de resgate.
    /// </summary>
    /// <param name="app">
    /// Construtor de rotas da aplicação.
    /// </param>
    /// <returns>
    /// Construtor de rotas com os endpoints registrados.
    /// </returns>
    public static IEndpointRouteBuilder MapRescueUnitRoutes(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rescue-units")
            .WithTags("Unidades de Resgate");

        group.MapGet("/{id:long}", GetByIdAsync)
            .WithName("GetRescueUnitById")
            .Produces<RescueUnitResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Busca uma unidade de resgate pelo identificador.
    /// </summary>
    /// <param name="id">
    /// Identificador da unidade de resgate.
    /// </param>
    /// <param name="service">
    /// Serviço responsável pela consulta das unidades.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição.
    /// </param>
    /// <returns>
    /// Dados da unidade de resgate solicitada.
    /// </returns>
    /// <response code="200">
    /// Unidade de resgate encontrada.
    /// </response>
    /// <response code="400">
    /// Identificador informado é inválido.
    /// </response>
    /// <response code="404">
    /// Unidade de resgate não encontrada.
    /// </response>
    private static async Task<IResult> GetByIdAsync(
        long id,
        RescueUnitQueryService service,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O identificador deve ser maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var unit = await service.GetByIdAsync(id, cancellationToken);

        return unit is null ? Results.Problem(
                title: "Unidade não encontrada.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(unit);
    }
}
