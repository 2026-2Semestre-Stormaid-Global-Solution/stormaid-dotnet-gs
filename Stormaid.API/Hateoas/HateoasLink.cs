namespace Stormaid.API.Hateoas;

/// <summary>
/// Representa um link de navegação ou ação HATEOAS.
/// </summary>
public sealed record HateoasLink
{
    /// <summary>
    /// Relação do link com o recurso.
    /// </summary>
    public string Rel { get; init; }

    /// <summary>
    /// Endereço do recurso ou da ação.
    /// </summary>
    public string Href { get; init; }

    /// <summary>
    /// Método HTTP utilizado pelo link.
    /// </summary>
    public string Method { get; init; }

    /// <summary>
    /// Inicializa um link HATEOAS.
    /// </summary>
    /// <param name="rel">Relação do link.</param>
    /// <param name="href">Endereço do recurso.</param>
    /// <param name="method">Método HTTP.</param>
    public HateoasLink(
        string rel,
        string href,
        string method)
    {
        Rel = rel;
        Href = href;
        Method = method;
    }
}