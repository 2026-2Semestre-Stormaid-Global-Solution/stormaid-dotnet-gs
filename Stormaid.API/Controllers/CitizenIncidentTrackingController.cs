using System.Globalization;
using System.Security.Claims;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;

namespace Stormaid.API.Controllers;

/// <summary>
/// Consultas de acompanhamento de ocorrências
/// pertencentes ao cidadão autenticado.
/// </summary>
public static class CitizenIncidentTrackingController
{
    /// <summary>
    /// Registra os endpoints de acompanhamento de ocorrências
    /// destinados aos cidadãos autenticados.
    /// </summary>
    /// <param name="app">
    /// Construtor de rotas utilizado para registrar os endpoints.
    /// </param>
    /// <returns>
    /// Construtor de rotas com os endpoints registrados.
    /// </returns>
    public static IEndpointRouteBuilder MapCitizenIncidentTrackingRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/citizens/incidents")
            .WithTags("Acompanhamento do Cidadão")
            .RequireAuthorization(policy =>
                policy.RequireRole("CITIZEN"))
            .RequireRateLimiting("InstitutionalApi");

        group.MapGet("/{javaIncidentId:long}/tracking", GetTrackingAsync)
            .WithName("GetCitizenIncidentTracking")
            .WithSummary("Acompanhar ocorrência do cidadão")
            .Produces<CitizenIncidentTrackingResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> GetTrackingAsync(
        long javaIncidentId,
        ClaimsPrincipal user,
        IncidentReferenceQueryService service,
        CancellationToken cancellationToken)
    {
        if (javaIncidentId <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "A ocorrência deve possuir um ID maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var claim = user.FindFirst("userId")?.Value;

        if (!long.TryParse(
                claim,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var citizenUserId) ||
            citizenUserId <= 0)
        {
            return Results.Forbid();
        }

        var incident = await service.GetByJavaIncidentIdAsync(
            javaIncidentId,
            cancellationToken);

        if (incident is null ||
            incident.RequesterUserId != citizenUserId)
        {
            return Results.NotFound();
        }

        var response = new CitizenIncidentTrackingResponse(
            incident.JavaIncidentId,
            incident.RegulationStatus.ToString(),
            incident.PublicStatusVersion
        );

        return Results.Ok(response);
    }
}
