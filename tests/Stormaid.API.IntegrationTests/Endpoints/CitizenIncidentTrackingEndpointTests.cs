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

/// <summary>
/// Testes HTTP do acompanhamento de ocorrências pelo cidadão,
/// com repositório simulado e sem acessar o Oracle.
/// </summary>
public sealed class CitizenIncidentTrackingEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const string TrackingPath =
        "/api/v1/citizens/incidents/307/tracking";

    private readonly StormaidWebApplicationFactory _factory;

    public CitizenIncidentTrackingEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTracking_ShouldReturn200_WhenCitizenOwnsIncident()
    {
        using var factory = CreateFactory(CreateIncident(requesterUserId: 123));
        using var client = CreateClient(factory, CreateToken("CITIZEN"));

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var body = json.RootElement;
        Assert.Equal(307L, body.GetProperty("javaIncidentId").GetInt64());
        Assert.Equal("CLOSED", body.GetProperty("regulationStatus").GetString());
        Assert.Equal(7L, body.GetProperty("publicStatusVersion").GetInt64());
        Assert.Equal(3, body.EnumerateObject().Count());
    }

    [Fact]
    public async Task GetTracking_ShouldReturn401_WhenTokenIsMissing()
    {
        using var factory = CreateFactory(CreateIncident(requesterUserId: 123));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTracking_ShouldReturn401_WhenTokenIsMalformed()
    {
        using var factory = CreateFactory(CreateIncident(requesterUserId: 123));
        using var client = CreateClient(factory, "invalid-token");

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("REGULATOR")]
    [InlineData("RESPONDER")]
    public async Task GetTracking_ShouldReturn403_WhenRoleIsInstitutional(
        string role)
    {
        using var factory = CreateFactory(CreateIncident(requesterUserId: 123));
        using var client = CreateClient(factory, CreateToken(role));

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTracking_ShouldReturn404_WhenIncidentBelongsToAnotherCitizen()
    {
        // O JWT de teste identifica o usuário 123.
        // A ocorrência simulada pertence ao usuário 903.
        using var factory = CreateFactory(CreateIncident(requesterUserId: 903));
        using var client = CreateClient(factory, CreateToken("CITIZEN"));

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTracking_ShouldReturn404_WhenIncidentDoesNotExist()
    {
        using var factory = CreateFactory(null);
        using var client = CreateClient(factory, CreateToken("CITIZEN"));

        using var response = await client.GetAsync(TrackingPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTracking_ShouldReturn400_WhenIncidentIdIsZero()
    {
        using var factory = CreateFactory(null);
        using var client = CreateClient(factory, CreateToken("CITIZEN"));

        using var response = await client.GetAsync(
            "/api/v1/citizens/incidents/0/tracking");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(
        IncidentReference? incident)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var repository = new Mock<IIncidentReferenceRepository>();

                repository.Setup(x => x.GetByJavaIncidentIdAsync(
                        307,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(incident);

                services.RemoveAll<IIncidentReferenceRepository>();
                services.AddScoped(_ => repository.Object);
            });
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        string? token = null)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static string CreateToken(string role)
    {
        // JwtTokenFactory utiliza userId 123 no token de teste.
        return JwtTokenFactory.CreateToken(
            JwtTokenFactory.TestSecret,
            role: role);
    }

    private static IncidentReference CreateIncident(long requesterUserId)
    {
        var now = DateTime.UtcNow;

        var incident = new IncidentReference(
            javaIncidentId: 307,
            requesterUserId: requesterUserId,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: null,
            longitude: null,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: now);

        incident.BeginReview(now);
        incident.AwaitDispatch(now);
        incident.MarkDispatched(now);
        incident.Close(now);

        // O fluxo atual avança a versão pública de 1 para 2
        // na primeira transição; seis avanços chegam à versão 7.
        for (var i = 0; i < 6; i++)
        {
            incident.AdvancePublicStatusVersion(now);
        }

        return incident;
    }
}
