using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.Exceptions;
using Stormaid.API.Hateoas;

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
    public static IEndpointRouteBuilder MapRescueUnitRoutes(this IEndpointRouteBuilder app)
    {
        
        var group = app.MapGroup("/api/v1/rescue-units")
            .WithTags("Unidades de Resgate")
            .RequireAuthorization("InstitutionalAccess")
            .RequireRateLimiting("InstitutionalApi");

        group.MapGet("/{id:long}", GetByIdAsync)
            .WithName("GetRescueUnitById")
            .WithSummary("Consultar unidade de resgate por ID")
            .WithDescription("Busca uma unidade de resgate pelo identificador informado.")
            .Produces<RescueUnitResponse>(
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




        group.MapGet("", GetPagedAsync)
            .WithName("GetRescueUnits")
            .WithSummary("Listar unidades de resgate")
            .WithDescription("Consulta as unidades de resgate com paginação.")
            .Produces<PagedResponse<RescueUnitResponse>>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);
        
        
        group.MapPost("", CreateAsync)
            .WithName("CreateRescueUnit")
            .WithSummary("Cadastrar unidade de resgate")
            .WithDescription(
                "Permite cadastrar uma unidade institucional de resgate.")
            .RequireAuthorization(policy =>
                policy.RequireRole("REGULATOR"))
            .Produces<RescueUnitResponse>(
                StatusCodes.Status201Created)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status409Conflict)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);


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
    /// <param name="includeLinks">
    /// Indica se a resposta deve incluir links HATEOAS.
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
    /// <response code="429">
    /// Limite de requisições excedido.
    /// </response>
    private static async Task<IResult> GetByIdAsync(
        long id,
        bool? includeLinks,
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

        if (unit is null)
        {
            return Results.Problem(
                title: "Unidade não encontrada.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (includeLinks != true)
        {
            return Results.Ok(unit);
        }

        var links = new List<HateoasLink>
        {
            new(
                "self",
                $"/api/v1/rescue-units/{unit.Id}",
                "GET"
            ),
            new(
                "collection",
                "/api/v1/rescue-units",
                "GET"
            )
        };

        var response = new HateoasResource<RescueUnitResponse>(
            unit,
            links
        );

        return Results.Ok(response);
    }
    
    /// <summary>
    /// Lista as unidades de resgate com paginação.
    /// </summary>
    /// <param name="page">
    /// Número da página solicitada, iniciando em 1.
    /// </param>
    /// <param name="pageSize">
    /// Quantidade de unidades por página, entre 1 e 100.
    /// </param>
    /// <param name="service">
    /// Serviço responsável pela consulta das unidades de resgate.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição.
    /// </param>
    /// <returns>
    /// Lista paginada das unidades de resgate.
    /// </returns>
    /// <response code="200">
    /// Unidades consultadas com sucesso.
    /// </response>
    /// <response code="400">
    /// Parâmetros de paginação inválidos.
    /// </response>
    /// <response code="429">
    /// Limite de requisições excedido.
    /// </response>
    private static async Task<IResult> GetPagedAsync(
        int? page,
        int? pageSize,
        RescueUnitQueryService service,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 10;

        if (currentPage < 1)
        {
            return Results.Problem(
                title: "Página inválida.",
                detail: "A página deve ser maior ou igual a 1.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (currentPageSize is < 1 or > 100)
        {
            return Results.Problem(
                title: "Tamanho de página inválido.",
                detail: "A quantidade de registros deve estar entre 1 e 100.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var offset = ((long)currentPage - 1) * currentPageSize;

        if (offset > int.MaxValue)
        {
            return Results.Problem(
                title: "Página fora do limite.",
                detail: "A página solicitada excede o limite permitido.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var result = await service.GetPagedAsync(
            currentPage,
            currentPageSize,
            cancellationToken
        );

        return Results.Ok(result);
    }
    
    
    /// <summary>
    /// Cadastra uma nova unidade institucional de resgate.
    /// </summary>
    /// <param name="request">Dados do cadastro.</param>
    /// <param name="service">Serviço de cadastro de unidades.</param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição.
    /// </param>
    /// <returns>Resultado da operação de cadastro.</returns>
    private static async Task<IResult> CreateAsync(
        CreateRescueUnitRequest request,
        RescueUnitCommandService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdUnit = await service.CreateAsync(
                request,
                cancellationToken
            );

            return Results.CreatedAtRoute(
                routeName: "GetRescueUnitById",
                routeValues: new { id = createdUnit.Id },
                value: createdUnit
            );
        }
        catch (RescueUnitCodeAlreadyExistsException ex)
        {
            return Results.Problem(
                title: "Código de unidade duplicado.",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict
            );
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    }


}
