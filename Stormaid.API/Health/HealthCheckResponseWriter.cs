using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Stormaid.API.Health;

/// <summary>
/// Responsável por padronizar as respostas dos Health Checks em JSON.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Escreve a resposta JSON contendo o estado geral
    /// e os resultados individuais das verificações.
    /// </summary>
    /// <param name="context">Contexto da requisição HTTP.</param>
    /// <param name="report">Relatório das verificações de saúde.</param>
    /// <returns>Task de escrita da resposta HTTP.</returns>
    public static Task WriteJsonResponse(
        HttpContext context,
        HealthReport report)
    {
        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(
                report.TotalDuration.TotalMilliseconds, 2),

            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    durationMs = Math.Round(
                        entry.Value.Duration.TotalMilliseconds, 2)
                })
        };

        return context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: context.RequestAborted
        );
    }
}