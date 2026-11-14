using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Stormaid.API.IntegrationTests.Factories;

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
}