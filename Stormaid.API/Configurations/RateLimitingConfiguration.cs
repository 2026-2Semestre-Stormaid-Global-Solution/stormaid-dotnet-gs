
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Stormaid.API.Configurations;

/// <summary>
/// Configuração das políticas de limitação de requisições
/// da API Storm Aid.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Registra a política de Rate Limiting para
    /// operações institucionais da API.
    /// </summary>
    /// <param name="services">
    /// Coleção de serviços da aplicação.
    /// </param>
    /// <returns>
    /// Coleção de serviços com Rate Limiting configurado.
    /// </returns>
    public static IServiceCollection AddStormaidRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("InstitutionalApi", context =>
            {
                var userId = context.User.FindFirst("userId")?.Value;

                var partitionKey = userId is not null
                    ? $"user:{userId}"
                    : $"ip:{context.Connection.RemoteIpAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(30),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }
                );
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;

                response.StatusCode = StatusCodes.Status429TooManyRequests;

                response.ContentType = "application/problem+json";

                if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Title = "Limite de requisições excedido.",
                        Detail = "Aguarde antes de realizar uma nova requisição.",
                        Status = StatusCodes.Status429TooManyRequests
                    },
                    cancellationToken: cancellationToken
                );
            };
        });

        return services;
    }
}
