
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
                    
                    document.Info = new()
                    {
                        Title = "Storm Aid | API de Regulação",
                        Version = "v1",
                        Description = string.Join("\n", new[]
                        {
                            "## Sobre o Storm Aid",
                            "",
                            "O Storm Aid é uma plataforma de apoio à gestão de emergências, " +
                            "conectando solicitações dos cidadãos a equipes institucionais " +
                            "responsáveis pela regulação e pelo resgate.",
                            "",
                            "### Funcionalidades",
                            "",
                            "- Regulação e acompanhamento de ocorrências.",
                            "- Gerenciamento de unidades de resgate.",
                            "- Criação e atualização de despachos operacionais.",
                            "- Acompanhamento de ocorrências pelo cidadão.",
                            "- Integração com a API Java por JWT e mensageria.",
                            "",
                            "### Perfis de acesso",
                            "",
                            "- **CITIZEN:** consulta suas próprias ocorrências.",
                            "- **REGULATOR:** analisa ocorrências e gerencia decisões institucionais.",
                            "- **RESPONDER:** executa operações de atendimento e resgate.",
                            "",
                            "### Autenticação",
                            "",
                            "Utilize o botão **Authorize** e informe o JWT emitido " +
                            "pela API Java do Storm Aid."
                        })
                    };
                    
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
                                 .SelectMany(path => path.Operations ?? []))
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
