using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Stormaid.API.IntegrationTests.Factories;

/// <summary>
/// Simula o estado do Oracle durante os testes.
/// </summary>
public sealed class FakeOracleHealthCheck : IHealthCheck
{
    private readonly HealthStatus _status;

    public FakeOracleHealthCheck(HealthStatus status)
    {
        _status = status;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _status == HealthStatus.Healthy
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                "ORA_TEST_INTERNAL_ERROR"
            );

        return Task.FromResult(result);
    }
}