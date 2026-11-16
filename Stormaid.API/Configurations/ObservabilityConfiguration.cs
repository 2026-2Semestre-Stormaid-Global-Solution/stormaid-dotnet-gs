using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Stormaid.API.Configurations;

/// <summary>
/// Configura tracing e métricas OpenTelemetry
/// para a API institucional Storm Aid.
/// </summary>
public static class ObservabilityConfiguration
{
    /// <summary>
    /// Registra a instrumentação HTTP e os exporters
    /// de observabilidade da aplicação.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>Coleção de serviços configurada.</returns>
    public static IServiceCollection AddStormaidObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEnabled = configuration.GetValue<bool>("OpenTelemetry:ExportTracesToOtlp");

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService("Stormaid.API");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(
                            "/metrics");
                });

                tracing.AddHttpClientInstrumentation();

                if (otlpEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();

                metrics.AddHttpClientInstrumentation();

                metrics.AddPrometheusExporter();
            });

        return services;
    }
}