using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Stormaid.API.IntegrationTests.Factories;

namespace Stormaid.API.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração do endpoint raiz da API.
/// </summary>
public sealed class RootEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private readonly StormaidWebApplicationFactory _factory;

    public RootEndpointTests(StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_ShouldReturnApiInformation()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var json = document.RootElement;

        Assert.Equal(
            "Storm Aid API",
            json.GetProperty("name").GetString());

        Assert.Equal(
            "v1",
            json.GetProperty("version").GetString());
    }
}