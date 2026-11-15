using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Services;
using Stormaid.Domain.Enums;

namespace Stormaid.API.Controllers;

/// <summary>
/// Representa a resposta do início da análise
/// institucional de uma ocorrência.
/// </summary>
public sealed record RegulationReviewResponse(
    long JavaIncidentId,
    string RegulationStatus,
    long? ReviewedByRegulatorUserId
);

/// <summary>
/// Configura as rotas institucionais de consulta e
/// regulação das ocorrências recebidas da API Java.
/// </summary>
public static class RegulationController
{
    /// <summary>
    /// Registra os endpoints de consulta e análise
    /// das ocorrências institucionais.
    /// </summary>
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

        group.MapPost(
                "/{javaIncidentId:long}/review",
                BeginReviewAsync)
            .WithName("BeginRegulationIncidentReview")
            .WithSummary(
                "Iniciar análise institucional da ocorrência")
            .WithDescription(
                "Inicia a análise de uma ocorrência pendente, " +
                "registra o regulador autenticado e prepara " +
                "a atualização pública na Outbox. " +
                "A operação é idempotente quando a ocorrência " +
                "já está em análise.")
            .Produces<RegulationReviewResponse>(
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
                StatusCodes.Status409Conflict)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);


        group.MapPost(
                "/{javaIncidentId:long}/decisions",
                RegisterDecisionAsync)
            .WithName("RegisterRegulationDecision")
            .WithSummary("Registrar decisão regulatória")
            .WithDescription(
                "Registra uma decisão institucional sobre uma ocorrência em análise.")
            .Produces<RegulationDecisionResponse>(
                StatusCodes.Status201Created)
            .Produces<RegulationDecisionResponse>(
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
                StatusCodes.Status409Conflict)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests);


        return app;
    }

    /// <summary>
    /// Lista as ocorrências disponíveis na fila de regulação.
    /// Permite filtrar pelo status e consultar os resultados
    /// de maneira paginada.
    /// </summary>
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
    private static async Task<IResult> GetByJavaIncidentIdAsync(
        long javaIncidentId,
        IncidentReferenceQueryService service,
        CancellationToken cancellationToken)
    {
        if (javaIncidentId <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail:
                    "O identificador Java deve ser maior que zero.",
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

    /// <summary>
    /// Inicia a análise institucional de uma ocorrência
    /// utilizando a identidade do regulador autenticado.
    /// </summary>
    /// <param name="javaIncidentId">
    /// Identificador da ocorrência na API Java.
    /// </param>
    /// <param name="user">
    /// Usuário autenticado por meio do JWT validado.
    /// </param>
    /// <param name="service">
    /// Serviço responsável pelos comandos de regulação.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da requisição HTTP.
    /// </param>
    /// <returns>
    /// Estado da análise e identificador do regulador
    /// originalmente responsável pela ocorrência.
    /// </returns>
    private static async Task<IResult> BeginReviewAsync(
        long javaIncidentId,
        ClaimsPrincipal user,
        IncidentReferenceCommandService service,
        CancellationToken cancellationToken)
    {
        // 1 - Validar o identificador da ocorrência.

        if (javaIncidentId <= 0)
        {
            return Results.Problem(
                title: "Identificador inválido.",
                detail:
                    "O identificador Java deve ser maior que zero.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // 2 - Obter a identidade institucional
        // a partir das claims do JWT validado.

        var subject = user.FindFirst("sub")?.Value;
        var userIdClaim = user.FindFirst("userId")?.Value;

        // 3 - Validar o identificador numérico
        // do regulador recebido pela API Java.

        var validUserId = long.TryParse(
            userIdClaim,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var regulatorUserId
        );

        // 4 - Permitir subject em formato de e-mail,
        // conforme o contrato JWT atual do Java.
        //
        // Se o subject for numérico, exigir que ele
        // corresponda ao identificador userId.

        var hasNumericSubject = long.TryParse(
            subject,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var subjectId
        );

        // 5 - Rejeitar identidades ausentes,
        // inválidas ou numericamente inconsistentes.

        if (string.IsNullOrWhiteSpace(subject) ||
            !validUserId ||
            regulatorUserId <= 0 ||
            (hasNumericSubject && subjectId != regulatorUserId))
        {
            return Results.Problem(
                title: "Identidade institucional inválida.",
                detail:
                    "O token autenticado não contém " +
                    "uma identidade institucional válida.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        // 6 - Executar a análise institucional
        // e tratar os resultados de domínio.

        try
        {
            var result = await service.BeginReviewAsync(
                javaIncidentId,
                regulatorUserId,
                cancellationToken
            );

            // 7 - Retornar o regulador original,
            // inclusive nas solicitações idempotentes.

            var response = new RegulationReviewResponse(
                JavaIncidentId: result.JavaIncidentId,
                RegulationStatus:
                    result.RegulationStatus.ToString(),
                ReviewedByRegulatorUserId:
                    result.ReviewedByRegulatorUserId
            );

            return Results.Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(
                title: "Ocorrência não encontrada.",
                detail:
                    "A ocorrência solicitada não existe " +
                    "na base de regulação.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                title: "Conflito de atualização.",
                detail:
                    "A ocorrência foi modificada por outra " +
                    "operação. Consulte seu estado atualizado.",
                statusCode: StatusCodes.Status409Conflict
            );
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                title: "Transição de status não permitida.",
                detail:
                    "O estado atual da ocorrência não permite " +
                    "iniciar uma nova análise.",
                statusCode: StatusCodes.Status409Conflict
            );
        }
    }


private static async Task<IResult> RegisterDecisionAsync(
    long javaIncidentId,
    CreateRegulationDecisionRequest request,
    ClaimsPrincipal user,
    RegulationDecisionCommandService service,
    CancellationToken cancellationToken)
{
    if (javaIncidentId <= 0)
    {
        return Results.Problem(
            title: "Identificador inválido.",
            detail: "O identificador Java deve ser maior que zero.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var subject = user.FindFirst("sub")?.Value;
    var userIdClaim = user.FindFirst("userId")?.Value;

    var validUserId = long.TryParse(
        userIdClaim,
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var regulatorUserId);

    var hasNumericSubject = long.TryParse(
        subject,
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var subjectId);

    if (string.IsNullOrWhiteSpace(subject) ||
        !validUserId ||
        regulatorUserId <= 0 ||
        (hasNumericSubject && subjectId != regulatorUserId))
    {
        return Results.Problem(
            title: "Identidade institucional inválida.",
            detail: "O token autenticado não contém uma identidade institucional válida.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.DecisionType) ||
        !Enum.TryParse<RegulationDecisionType>(
            request.DecisionType,
            out var decisionType) ||
        !Enum.IsDefined(decisionType) ||
        request.DecisionType != decisionType.ToString())
    {
        return Results.Problem(
            title: "Tipo de decisão inválido.",
            detail: "Informe um tipo de decisão regulatória válido.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    ResponderInstitutionType? institutionType = null;

    if (request.InstitutionType is not null)
    {
        if (!Enum.TryParse<ResponderInstitutionType>(
                request.InstitutionType,
                out var parsedInstitution) ||
            !Enum.IsDefined(parsedInstitution) ||
            request.InstitutionType != parsedInstitution.ToString())
        {
            return Results.Problem(
                title: "Instituição inválida.",
                detail: "Informe uma instituição de atendimento válida.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        institutionType = parsedInstitution;
    }

    if (decisionType == RegulationDecisionType.DISPATCH_APPROVED &&
        !institutionType.HasValue)
    {
        return Results.Problem(
            title: "Instituição obrigatória.",
            detail: "A aprovação do despacho exige uma instituição de atendimento.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (decisionType != RegulationDecisionType.DISPATCH_APPROVED &&
        institutionType.HasValue)
    {
        return Results.Problem(
            title: "Instituição não permitida.",
            detail: "A instituição deve ser nula quando o despacho não é aprovado.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(request.Justification))
    {
        return Results.Problem(
            title: "Justificativa obrigatória.",
            detail: "Informe a justificativa da decisão regulatória.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var justification = request.Justification.Trim();

    if (justification.Length is < 10 or > 1000)
    {
        return Results.Problem(
            title: "Justificativa inválida.",
            detail: "A justificativa deve conter entre 10 e 1000 caracteres.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        var result = await service.RegisterAsync(
            javaIncidentId,
            regulatorUserId,
            decisionType,
            institutionType,
            justification,
            cancellationToken);

        var response = new RegulationDecisionResponse(
            DecisionId: result.DecisionId,
            JavaIncidentId: result.JavaIncidentId,
            DecisionType: result.DecisionType.ToString(),
            InstitutionType: result.InstitutionType?.ToString(),
            RegulationStatus: result.RegulationStatus.ToString(),
            PublicStatusVersion: result.PublicStatusVersion,
            RegulatorUserId: result.RegulatorUserId,
            Changed: result.Changed);

        return result.Changed
            ? Results.Json(
                response,
                statusCode: StatusCodes.Status201Created)
            : Results.Ok(response);
    }
    catch (KeyNotFoundException)
    {
        return Results.Problem(
            title: "Ocorrência não encontrada.",
            detail: "A ocorrência solicitada não existe na base de regulação.",
            statusCode: StatusCodes.Status404NotFound);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Problem(
            title: "Conflito de atualização.",
            detail: "A ocorrência foi modificada por outra operação.",
            statusCode: StatusCodes.Status409Conflict);
    }
    catch (InvalidOperationException)
    {
        return Results.Problem(
            title: "Decisão não permitida.",
            detail: "O estado atual da ocorrência não permite registrar essa decisão.",
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

}
