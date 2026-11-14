using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using System.Net.Http.Json;

namespace Stormaid.API.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração do cadastro de unidades de resgate.
/// </summary>
public sealed class RescueUnitCreateEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private readonly StormaidWebApplicationFactory _factory;

    private const string Endpoint = "/api/v1/rescue-units";

    public RescueUnitCreateEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // 1 - Requisição sem autenticação

    [Fact]
    public async Task CreateRescueUnit_ShouldReturn401_WhenTokenIsMissing()
    {
        using var client = CreateClient();

        using var content = CreateContent();

        using var response = await client.PostAsync(
            Endpoint,
            content
        );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );
    }

    // 2 - Perfis sem permissão para cadastrar

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("RESPONDER")]
    public async Task CreateRescueUnit_ShouldReturn403_WhenRoleIsNotRegulator(
        string role)
    {
        using var client = CreateClient();

        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: role
        );

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );

        using var content = CreateContent();

        using var response = await client.PostAsync(
            Endpoint,
            content
        );

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode
        );
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            }
        );
    }

    private static StringContent CreateContent()
    {
        return new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json"
        );
    }
    
    
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task CreateRescueUnit_ShouldReturn400_WhenCapacityIsInvalid(
        int capacity)
    {
        // Arrange
        using var client = CreateClient();

        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR"
        );

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            unitCode = "SA-FD-INVALID",
            displayName = "Unidade com Capacidade Inválida",
            institutionType = "FIRE_DEPARTMENT",
            capacity,
            supportsAccessibleTransport = true
        };

        // Act
        using var response = await client.PostAsJsonAsync(
            Endpoint,
            request
        );

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );
    }

}
