
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;

namespace Stormaid.API.Controllers;

/// <summary>
/// Rotas responsáveis pela criação dos despachos
/// operacionais das ocorrências reguladas.
/// </summary>
public static class IncidentDispatchController
{
    public static IEndpointRouteBuilder MapIncidentDispatchRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dispatches")
            .WithTags("Despachos Operacionais")
            .RequireAuthorization("InstitutionalAccess")
            .RequireAuthorization(policy =>
                policy.RequireRole("REGULATOR"))
            .RequireRateLimiting("InstitutionalApi");

        group.MapPost("", CreateAsync)
            .WithName("CreateIncidentDispatch")
            .WithSummary("Criar despacho operacional")
            .WithDescription(
                "Cria um despacho para uma ocorrência com " +
                "decisão regulatória aprovada. Valida a " +
                "disponibilidade da unidade, registra o " +
                "histórico e prepara o evento na Outbox.")
            .Produces<IncidentDispatchResponse>(
                StatusCodes.Status201Created)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status401Unauthorized)
            .ProducesProblem(
                StatusCodes.Status403Forbidden)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status409Conflict)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateIncidentDispatchRequest request,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {

        if (request.JavaIncidentId <= 0 ||
            request.RescueUnitId <= 0)
        {
            return Results.Problem(
                title: "Identificadores inválidos.",
                detail:
                    "A ocorrência e a unidade de resgate " +
                    "devem possuir identificadores maiores que zero.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var subject = user.FindFirst("sub")?.Value;
        var userIdClaim = user.FindFirst("userId")?.Value;

        var validUserId = long.TryParse(
            userIdClaim,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var regulatorUserId
        );

        var hasNumericSubject = long.TryParse(
            subject,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var subjectId
        );

        if (string.IsNullOrWhiteSpace(subject) ||
            !validUserId ||
            regulatorUserId <= 0 ||
            (hasNumericSubject &&
             subjectId != regulatorUserId))
        {
            return Results.Problem(
                title: "Identidade institucional inválida.",
                detail:
                    "O token autenticado não contém uma " +
                    "identidade institucional válida.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        try
        {
            var dispatch = await service.CreateAsync(
                request,
                regulatorUserId,
                cancellationToken
            );

            return Results.Json(
                dispatch,
                statusCode: StatusCodes.Status201Created
            );
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Registro não encontrado.",
                detail:
                    "A ocorrência ou a unidade de resgate " +
                    "informada não foi encontrada.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                title: "Conflito de concorrência.",
                detail:
                    "Os dados foram modificados por outra " +
                    "operação. Consulte o estado atualizado.",
                statusCode: StatusCodes.Status409Conflict
            );
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Despacho não permitido.",
                detail:
                    "Verifique a decisão regulatória, " +
                    "o status da ocorrência e a " +
                    "disponibilidade da unidade.",
                statusCode: StatusCodes.Status409Conflict
            );
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    }
}
