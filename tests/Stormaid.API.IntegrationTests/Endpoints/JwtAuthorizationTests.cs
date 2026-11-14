
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
/// Testes de integração da autenticação e autorização JWT.
/// </summary>
public sealed class JwtAuthorizationTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const string ListPath =
        "/api/v1/rescue-units?page=1&pageSize=10";

    private const string ByIdPath =
        "/api/v1/rescue-units/10";

    private readonly StormaidWebApplicationFactory _factory;

    public JwtAuthorizationTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // 1 - Requisição sem JWT

    [Theory]
    [InlineData("/api/v1/rescue-units")]
    [InlineData("/api/v1/rescue-units/10")]
    public async Task GetRescueUnit_ShouldReturn401_WhenTokenIsMissing(
        string path)
    {
        var status = await GetStatusAsync(
            token: null,
            path: path);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 2 - Token malformado

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenTokenIsMalformed()
    {
        var status = await GetStatusAsync(
            "not-a-valid-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 3 - Assinatura inválida

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenSignatureIsInvalid()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: new string('y', 32),
            role: "REGULATOR");

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 4 - Issuer ou Audience incorretos

    [Theory]
    [InlineData("invalid-issuer", "stormaid-services")]
    [InlineData("stormaid-api", "invalid-audience")]
    public async Task GetRescueUnit_ShouldReturn401_WhenIssuerOrAudienceIsInvalid(
        string issuer,
        string audience)
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR",
            issuer: issuer,
            audience: audience);

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 5 - Token expirado

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenTokenIsExpired()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-2));

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 6 - Token sem expiração

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenExpirationIsMissing()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR",
            includeExpiration: false);

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 7 - Token ainda não válido

    [Fact]
    public async Task GetRescueUnit_ShouldReturn401_WhenNotBeforeIsInFuture()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "REGULATOR",
            notBeforeUtc: DateTime.UtcNow.AddMinutes(5));

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // 8 - Role sem permissão

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("UNKNOWN")]
    public async Task GetRescueUnit_ShouldReturn403_WhenRoleIsNotAllowed(
        string role)
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: role);

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    // 9 - Citizen não pode consultar unidade por ID

    [Fact]
    public async Task GetRescueUnitById_ShouldReturn403_WhenRoleIsCitizen()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: "CITIZEN");

        var status = await GetStatusAsync(
            token,
            ByIdPath);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    // 10 - Token sem roles

    [Fact]
    public async Task GetRescueUnit_ShouldReturn403_WhenRolesAreMissing()
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: null);

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    // 11 - Perfis institucionais autorizados

    [Theory]
    [InlineData("REGULATOR")]
    [InlineData("RESPONDER")]
    public async Task GetRescueUnit_ShouldReturn200_WhenRoleIsAuthorized(
        string role)
    {
        var token = JwtTokenFactory.CreateToken(
            secret: JwtTokenFactory.TestSecret,
            role: role);

        var status = await GetStatusAsync(token);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    /// <summary>
    /// Executa uma requisição HTTP autenticada em ambiente
    /// isolado, sem acesso ao Oracle.
    /// </summary>
    private async Task<HttpStatusCode> GetStatusAsync(
        string? token,
        string path = ListPath)
    {
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

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }

        using var response = await client.GetAsync(path);

        return response.StatusCode;
    }
}