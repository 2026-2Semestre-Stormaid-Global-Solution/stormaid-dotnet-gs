
using Microsoft.OpenApi;

namespace Stormaid.API.Configurations;

/// <summary>
/// Configuração da documentação OpenAPI do Storm Aid.
/// </summary>
public static class OpenApiConfiguration
{
    /// <summary>
    /// Registra a documentação OpenAPI e configura
    /// a autenticação Bearer JWT no Swagger.
    /// </summary>
    /// <param name="services">
    /// Coleção de serviços da aplicação.
    /// </param>
    /// <returns>
    /// Coleção de serviços com o OpenAPI configurado.
    /// </returns>
    public static IServiceCollection AddStormaidOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(
                (document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();

                    document.Components.SecuritySchemes =
                        new Dictionary<string, IOpenApiSecurityScheme>
                        {
                            ["Bearer"] = new OpenApiSecurityScheme
                            {
                                Type = SecuritySchemeType.Http,
                                Scheme = "bearer",
                                BearerFormat = "JWT",
                                In = ParameterLocation.Header,
                                Description =
                                    "Informe o JWT gerado pela API Java."
                            }
                        };

                    foreach (var operation in document.Paths.Values
                                 .SelectMany(path => path.Operations))
                    {
                        operation.Value.Security ??= [];

                        operation.Value.Security.Add(
                            new OpenApiSecurityRequirement
                            {
                                [
                                    new OpenApiSecuritySchemeReference("Bearer", document)
                                ] = []
                            }
                        );
                    }

                    return Task.CompletedTask;
                }
            );
        });

        return services;
    }
}
