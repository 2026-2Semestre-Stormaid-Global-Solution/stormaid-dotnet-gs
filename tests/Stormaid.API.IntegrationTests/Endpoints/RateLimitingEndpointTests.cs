
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using Stormaid.Application.Interfaces;

namespace Stormaid.API.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração da política de Rate Limiting.
/// </summary>
public sealed class RateLimitingEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private readonly StormaidWebApplicationFactory _factory;

    public RateLimitingEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRescueUnits_ShouldReturn429_WhenRateLimitIsExceeded()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRescueUnitRepository>();

                services.AddScoped<
                    IRescueUnitRepository,
                    FakeRescueUnitRepository>();
            });
        });

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        const string endpoint = "/api/v1/rescue-units";

        // Act / Assert
        for (var i = 1; i <= 10; i++)
        {
            using var response = await client.GetAsync(endpoint);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }

        using var rejectedResponse = await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejectedResponse.StatusCode);
    }
}
