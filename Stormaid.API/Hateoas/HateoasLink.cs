namespace Stormaid.API.Hateoas;

public sealed record HateoasLink(
    string Rel,
    string Href,
    string Method
);