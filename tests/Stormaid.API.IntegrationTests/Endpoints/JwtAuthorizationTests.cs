using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Stormaid.API.IntegrationTests.Factories;
using System.Net.Http.Headers;
using Stormaid.API.IntegrationTests.Helpers;

namespace Stormaid.API.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração de autenticação e autorização JWT.
/// </summary>
public sealed class JwtAuthorizationTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private readonly StormaidWebApplicationFactory _factory;

    public JwtAuthorizationTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenTokenIsMissing()
    {
        // Arrange
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Act
        using var response = await client.GetAsync(
            "/api/v1/rescue-units/10");

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
    
    
    [Fact]
    public async Task GetRescueUnit_ShouldReturn403_WhenCitizenRoleIsUsed()
    {
        // Arrange
        var token = JwtTokenFactory.CreateToken(
            secret: new string('x', 32),
            role: "CITIZEN");

        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        using var response = await client.GetAsync(
            "/api/v1/rescue-units/10");

        // Assert
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

}