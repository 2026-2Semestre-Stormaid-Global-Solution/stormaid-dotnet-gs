using System.Security.Claims;
using Stormaid.Application.DTOs.Response;
using Stormaid.Domain.Enums;

namespace Stormaid.API.Hateoas;

/// <summary>
/// Gera links HATEOAS para despachos operacionais.
/// </summary>
public static class IncidentDispatchHateoasLinks
{
    /// <summary>
    /// Cria a representação do despacho com as ações
    /// permitidas pelo estado e pelo perfil autenticado.
    /// </summary>
    /// <param name="dispatch">Dados do despacho.</param>
    /// <param name="user">Usuário autenticado.</param>
    /// <returns>Despacho com seus links HATEOAS.</returns>
    public static HateoasResource<IncidentDispatchResponse> Build(
        IncidentDispatchResponse dispatch,
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(user);

        var links = new List<HateoasLink>
        {
            new(
                "rescue-unit",
                $"/api/v1/rescue-units/{dispatch.RescueUnitId}",
                "GET")
        };

        var isCancellable = dispatch.Status is
            DispatchStatus.PENDING_ACCEPTANCE or
            DispatchStatus.ACCEPTED or
            DispatchStatus.EN_ROUTE or
            DispatchStatus.ON_SITE;

        if (user.IsInRole("REGULATOR") && isCancellable)
        {
            links.Add(new HateoasLink(
                "cancel",
                $"/api/v1/dispatches/{dispatch.Id}/cancel",
                "POST"));
        }

        return new HateoasResource<IncidentDispatchResponse>(
            dispatch,
            links);
    }
}