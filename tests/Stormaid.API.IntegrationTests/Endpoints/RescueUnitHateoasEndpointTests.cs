
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Endpoints;

public sealed class RescueUnitHateoasEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const string Endpoint = "/api/v1/rescue-units/10";

    private readonly StormaidWebApplicationFactory _factory;

    public RescueUnitHateoasEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("?includeLinks=false")]
    public async Task GetById_ShouldPreserveOriginalResponse_WhenLinksAreNotRequested(
        string query)
    {
        using var factory = CreateFactory(CreateUnit());
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(Endpoint + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(10L, root.GetProperty("id").GetInt64());
        Assert.Equal(
            "BOM-010",
            root.GetProperty("unitCode").GetString());

        Assert.False(root.TryGetProperty("data", out _));
        Assert.False(root.TryGetProperty("links", out _));
    }

    [Fact]
    public async Task GetById_ShouldReturnHateoasLinks_WhenRequested()
    {
        using var factory = CreateFactory(CreateUnit());
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            Endpoint + "?includeLinks=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;
        var data = root.GetProperty("data");
        var links = root.GetProperty("links");

        Assert.Equal(10L, data.GetProperty("id").GetInt64());
        Assert.Equal(
            "BOM-010",
            data.GetProperty("unitCode").GetString());

        Assert.Equal(2, links.GetArrayLength());

        var self = links[0];

        Assert.Equal("self", self.GetProperty("rel").GetString());
        Assert.Equal(
            Endpoint,
            self.GetProperty("href").GetString());
        Assert.Equal("GET", self.GetProperty("method").GetString());

        var collection = links[1];

        Assert.Equal(
            "collection",
            collection.GetProperty("rel").GetString());
        Assert.Equal(
            "/api/v1/rescue-units",
            collection.GetProperty("href").GetString());
        Assert.Equal(
            "GET",
            collection.GetProperty("method").GetString());
    }

    [Fact]
    public async Task GetById_ShouldReturn404_WhenUnitDoesNotExist()
    {
        using var factory = CreateFactory(null);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            Endpoint + "?includeLinks=true");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn400_WhenIdIsInvalid()
    {
        using var factory = CreateFactory(null);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/api/v1/rescue-units/0?includeLinks=true");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn401_WhenTokenIsMissing()
    {
        using var factory = CreateFactory(CreateUnit());
        using var client = CreateClient(factory, null);

        using var response = await client.GetAsync(
            Endpoint + "?includeLinks=true");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn403_WhenCitizenRequestsLinks()
    {
        using var factory = CreateFactory(CreateUnit());
        using var client = CreateClient(factory, "CITIZEN");

        using var response = await client.GetAsync(
            Endpoint + "?includeLinks=true");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(RescueUnit? unit)
    {
        var repository = new Mock<IRescueUnitRepository>();

        repository.Setup(repo => repo.GetByIdAsync(
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRescueUnitRepository>();

                services.AddScoped<IRescueUnitRepository>(
                    _ => repository.Object);
            });
        });
    }

    private static RescueUnit CreateUnit()
    {
        var unit = new RescueUnit(
            "BOM-010",
            "Equipe de Bombeiros",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            4,
            true,
            DateTime.UtcNow);

        typeof(RescueUnit)
            .GetProperty(nameof(RescueUnit.Id))!
            .SetValue(unit, 10L);

        return unit;
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        string? role = "REGULATOR")
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        if (role is not null)
        {
            var token = JwtTokenFactory.CreateToken(
                secret: JwtTokenFactory.TestSecret,
                role: role);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }

        return client;
    }
}
