using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Stormaid.API.IntegrationTests.Factories;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Stormaid.API.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração dos endpoints de Health Checks.
/// </summary>
public sealed class HealthCheckEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private readonly StormaidWebApplicationFactory _factory;

    public HealthCheckEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthLive_ShouldReturnHealthy()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var json = document.RootElement;

        Assert.Equal(
            "Healthy",
            json.GetProperty("status").GetString());

        var checks = json.GetProperty("checks");

        Assert.True(checks.TryGetProperty("self", out _));
        Assert.False(checks.TryGetProperty("oracle", out _));
    }
    
    
    [Fact]
    public async Task GetHealthReady_ShouldReturnHealthy_WhenOracleIsAvailable()
    {
        // Arrange
        using var factory = CreateFactoryWithOracleStatus(
            HealthStatus.Healthy);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Act
        using var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var json = document.RootElement;
        var checks = json.GetProperty("checks");

        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        Assert.Equal(
            "Healthy",
            checks.GetProperty("oracle").GetProperty("status").GetString());

        Assert.Single(checks.EnumerateObject());
    }

    
    [Fact]
    public async Task GetHealthReady_ShouldReturn503_WhenOracleIsUnavailable()
    {
        // Arrange
        using var factory = CreateFactoryWithOracleStatus(
            HealthStatus.Unhealthy);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Act
        using var response = await client.GetAsync("/health/ready");

        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        using var document = JsonDocument.Parse(body);

        var json = document.RootElement;

        Assert.Equal(
            "Unhealthy",
            json.GetProperty("status").GetString());

        Assert.Equal(
            "Unhealthy",
            json.GetProperty("checks")
                .GetProperty("oracle")
                .GetProperty("status")
                .GetString());

        // Informações internas não devem aparecer no JSON.
        Assert.DoesNotContain("ORA_TEST_INTERNAL_ERROR", body);

        // Liveness continua funcionando mesmo com Oracle indisponível.
        using var liveResponse = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // Health geral deve refletir a indisponibilidade do Oracle.
        using var generalResponse = await client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            generalResponse.StatusCode);
    }

    
    
    private WebApplicationFactory<Program> CreateFactoryWithOracleStatus(
        HealthStatus status)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                {
                    var original = options.Registrations
                                       .SingleOrDefault(check => check.Name == "oracle")
                                   ?? throw new InvalidOperationException(
                                       "Health Check 'oracle' não encontrado.");

                    options.Registrations.Remove(original);

                    options.Registrations.Add(
                        new HealthCheckRegistration(
                            name: "oracle",
                            factory: _ => new FakeOracleHealthCheck(status),
                            failureStatus: HealthStatus.Unhealthy,
                            tags: ["ready"]
                        )
                    );
                });
            });
        });
    }

}