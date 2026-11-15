using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;

namespace Stormaid.API.Controllers;

/// <summary>
/// Rotas responsáveis pela criação e aceitação dos despachos
/// operacionais das ocorrências reguladas.
/// </summary>
public static class IncidentDispatchController
{
    /// <summary>
    /// Registra as rotas responsáveis pelo gerenciamento
    /// dos despachos operacionais.
    /// </summary>
    /// <param name="app">Construtor de rotas da aplicação.</param>
    /// <returns>Construtor de rotas com os endpoints registrados.</returns>
    public static IEndpointRouteBuilder MapIncidentDispatchRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dispatches")
            .WithTags("Despachos Operacionais")
            .RequireAuthorization("InstitutionalAccess")
            .RequireRateLimiting("InstitutionalApi");

        group.MapPost("", CreateAsync)
            .WithName("CreateIncidentDispatch")
            .WithSummary("Criar despacho operacional")
            .WithDescription(
                "Cria um despacho para uma ocorrência com " +
                "decisão regulatória aprovada. Valida a " +
                "disponibilidade da unidade, registra o " +
                "histórico e prepara o evento na Outbox.")
            .RequireAuthorization(policy => policy.RequireRole("REGULATOR"))
            .Produces<IncidentDispatchResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{id:long}/accept", AcceptAsync)
            .WithName("AcceptIncidentDispatch")
            .WithSummary("Aceitar despacho operacional")
            .WithDescription(
                "Permite que um socorrista vinculado à unidade aceite " +
                "um despacho pendente, registrando a transição no histórico.")
            .RequireAuthorization(policy => policy.RequireRole("RESPONDER"))
            .Produces<IncidentDispatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{id:long}/start-route", StartRouteAsync)
            .WithName("StartIncidentDispatchRoute")
            .WithSummary("Iniciar deslocamento da equipe")
            .WithDescription(
                "Permite que o socorrista responsável pelo despacho inicie " +
                "o deslocamento, registra o histórico e publica EN_ROUTE " +
                "na Outbox para sincronização com o Java.")
            .RequireAuthorization(policy => policy.RequireRole("RESPONDER"))
            .Produces<IncidentDispatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);



        group.MapPost("/{id:long}/arrive", ArriveAsync)
            .WithName("ArriveIncidentDispatch")
            .WithSummary("Registrar chegada da equipe ao local")
            .WithDescription(
                "Permite que o socorrista responsável registre a chegada " +
                "ao local, atualiza o histórico e publica ON_SITE na Outbox " +
                "para sincronização com a API Java.")
            .RequireAuthorization(policy => policy.RequireRole("RESPONDER"))
            .Produces<IncidentDispatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);


        group.MapPost("/{id:long}/resolve", ResolveAsync)
            .WithName("ResolveIncidentDispatch")
            .WithSummary("Concluir atendimento da ocorrência")
            .WithDescription(
                "Permite ao socorrista responsável finalizar um despacho ON_SITE, " +
                "registrar o histórico, liberar a unidade de resgate e publicar " +
                "RESOLVED na Outbox para a API Java.")
            .RequireAuthorization(policy => policy.RequireRole("RESPONDER"))
            .Produces<IncidentDispatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateIncidentDispatchRequest request,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {
        if (request.JavaIncidentId <= 0 || request.RescueUnitId <= 0)
        {
            return Results.Problem(
                title: "Identificadores inválidos.",
                detail: "A ocorrência e a unidade de resgate devem possuir identificadores maiores que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryGetInstitutionalUserId(user, out var regulatorUserId))
        {
            return InvalidInstitutionalIdentity();
        }

        try
        {
            var dispatch = await service.CreateAsync(request, regulatorUserId, cancellationToken);
            return Results.Json(dispatch, statusCode: StatusCodes.Status201Created);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Registro não encontrado.",
                detail: "A ocorrência ou a unidade de resgate informada não foi encontrada.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Despacho não permitido.",
                detail: "Verifique a decisão regulatória, o status da ocorrência e a disponibilidade da unidade.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> AcceptAsync(
        long id,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O despacho deve possuir identificador maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryGetInstitutionalUserId(user, out var responderUserId))
        {
            return InvalidInstitutionalIdentity();
        }

        try
        {
            var dispatch = await service.AcceptAsync(id, responderUserId, cancellationToken);
            return Results.Ok(dispatch);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Despacho não encontrado.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(
                title: "Unidade não autorizada.",
                detail: "O socorrista não possui vínculo ativo com a unidade do despacho.",
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Transição de despacho inválida.",
                detail: "O despacho não pode ser aceito no estado atual ou já pertence a outro responsável.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> StartRouteAsync(
        long id,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O despacho deve possuir identificador maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryGetInstitutionalUserId(user, out var responderUserId))
        {
            return InvalidInstitutionalIdentity();
        }

        try
        {
            var dispatch = await service.StartRouteAsync(
                id, responderUserId, cancellationToken);
            return Results.Ok(dispatch);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Registro não encontrado.",
                detail: "O despacho ou a ocorrência vinculada não foi encontrado.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(
                title: "Socorrista não autorizado.",
                detail: "Apenas quem aceitou o despacho e mantém vínculo ativo " +
                        "com a unidade pode iniciar o deslocamento.",
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Transição de despacho inválida.",
                detail: "O despacho deve estar aceito e a ocorrência deve " +
                        "estar despachada.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ArriveAsync(
        long id,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O despacho deve possuir identificador maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryGetInstitutionalUserId(user, out var responderUserId))
        {
            return InvalidInstitutionalIdentity();
        }

        try
        {
            var dispatch = await service.ArriveAsync(
                id, responderUserId, cancellationToken);
            return Results.Ok(dispatch);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Registro não encontrado.",
                detail: "O despacho ou a ocorrência vinculada não foi encontrado.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(
                title: "Socorrista não autorizado.",
                detail: "Apenas quem aceitou o despacho e mantém vínculo ativo " +
                        "com a unidade pode registrar a chegada.",
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Transição de despacho inválida.",
                detail: "O despacho deve estar em deslocamento e a ocorrência " +
                        "deve permanecer despachada.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ResolveAsync(
        long id,
        ClaimsPrincipal user,
        IncidentDispatchCommandService service,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail: "O despacho deve possuir identificador maior que zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryGetInstitutionalUserId(user, out var responderUserId))
        {
            return InvalidInstitutionalIdentity();
        }

        try
        {
            var dispatch = await service.ResolveAsync(
                id, responderUserId, cancellationToken);
            return Results.Ok(dispatch);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Registro não encontrado.",
                detail: "O despacho, a ocorrência ou a unidade vinculada não foi encontrado.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(
                title: "Socorrista não autorizado.",
                detail: "Somente quem aceitou o despacho e mantém vínculo ativo " +
                        "com a unidade pode concluir o atendimento.",
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Conclusão de atendimento não permitida.",
                detail: "O despacho deve estar no local, a ocorrência despachada " +
                        "e a unidade ativa e ocupada.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Dados inválidos.",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static bool TryGetInstitutionalUserId(ClaimsPrincipal user, out long userId)
    {
        var subject = user.FindFirst("sub")?.Value;
        var userIdClaim = user.FindFirst("userId")?.Value;

        var validUserId = long.TryParse(
            userIdClaim,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out userId);

        var hasNumericSubject = long.TryParse(
            subject,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var subjectId);

        return !string.IsNullOrWhiteSpace(subject) &&
               validUserId && userId > 0 &&
               (!hasNumericSubject || subjectId == userId);
    }

    private static IResult InvalidInstitutionalIdentity()
    {
        return Results.Problem(
            title: "Identidade institucional inválida.",
            detail: "O token autenticado não contém uma identidade institucional válida.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    private static IResult ConcurrencyConflict()
    {
        return Results.Problem(
            title: "Conflito de concorrência.",
            detail: "Os dados foram modificados por outra operação. Consulte o estado atualizado.",
            statusCode: StatusCodes.Status409Conflict);
    }
}
