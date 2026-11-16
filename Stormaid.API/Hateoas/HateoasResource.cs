namespace Stormaid.API.Hateoas;

public sealed record HateoasResource<T>(
    T Data,
    IReadOnlyList<HateoasLink> Links
);