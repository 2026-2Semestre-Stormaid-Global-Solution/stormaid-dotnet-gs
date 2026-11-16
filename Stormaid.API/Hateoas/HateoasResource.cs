namespace Stormaid.API.Hateoas;

/// <summary>
/// Representa um recurso acompanhado de links HATEOAS.
/// </summary>
/// <typeparam name="T">
/// Tipo dos dados retornados pela API.
/// </typeparam>
public sealed record HateoasResource<T>
{
    /// <summary>
    /// Dados do recurso solicitado.
    /// </summary>
    public T Data { get; init; }

    /// <summary>
    /// Links de navegação disponíveis para o recurso.
    /// </summary>
    public IReadOnlyList<HateoasLink> Links { get; init; }

    /// <summary>
    /// Inicializa um recurso com seus links HATEOAS.
    /// </summary>
    /// <param name="data">Dados do recurso.</param>
    /// <param name="links">Links de navegação.</param>
    public HateoasResource(
        T data,
        IReadOnlyList<HateoasLink> links)
    {
        Data = data;
        Links = links;
    }
}