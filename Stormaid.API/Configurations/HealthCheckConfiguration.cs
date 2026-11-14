using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stormaid.Infrastructure.Data;

namespace Stormaid.API.Configurations;

/// <summary>
/// Configuração dos Health Checks da API Storm Aid.
/// </summary>
public static class HealthCheckConfiguration
{
    /// <summary>
    /// Registra os Health Checks da aplicação
    /// e da conexão com o banco Oracle.
    /// </summary>
    /// <param name="services">
    /// Coleção de serviços da aplicação.
    /// </param>
    /// <returns>
    /// Coleção de serviços com Health Checks configurados.
    /// </returns>
    public static IServiceCollection AddStormaidHealthChecks(
        this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: ["live"]
            )
            .AddDbContextCheck<StormaidDbContext>(
                name: "oracle",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]
            );

        return services;
    }
}