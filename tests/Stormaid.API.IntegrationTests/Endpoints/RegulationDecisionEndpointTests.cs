using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Endpoints;

public sealed class RegulationDecisionEndpointTests : IClassFixture<StormaidWebApplicationFactory>
{
    private const long JavaIncidentId = 8;
    private const long IncidentReferenceId = 50;
    private const long RegulatorUserId = 123;
    private const long DecisionId = 71;

    private const string DecisionPath = "/api/v1/regulation/incidents/8/decisions";
    private const string Justification = "Ocorrência analisada pela equipe de regulação.";

    private readonly StormaidWebApplicationFactory _factory;

    public RegulationDecisionEndpointTests(StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("DISPATCH_APPROVED", "FIRE_DEPARTMENT", "AWAITING_DISPATCH", 2L)]
    [InlineData("NO_DISPATCH_REQUIRED", null, "CLOSED", 2L)]
    [InlineData("MORE_INFORMATION_REQUIRED", null, "UNDER_REVIEW", 1L)]
    public async Task RegisterDecision_ShouldReturn201_WhenDecisionIsValid(
        string decisionType,
        string? institutionType,
        string expectedStatus,
        long expectedVersion)
    {
        var incident = CreateIncident();

        using var factory = CreateFactory(
            incident,
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            decisionType,
            institutionType);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var body = json.RootElement;

        Assert.Equal(DecisionId, body.GetProperty("decisionId").GetInt64());
        Assert.Equal(JavaIncidentId, body.GetProperty("javaIncidentId").GetInt64());
        Assert.Equal(decisionType, body.GetProperty("decisionType").GetString());
        Assert.Equal(expectedStatus, body.GetProperty("regulationStatus").GetString());
        Assert.Equal(expectedVersion, body.GetProperty("publicStatusVersion").GetInt64());
        Assert.Equal(RegulatorUserId, body.GetProperty("regulatorUserId").GetInt64());
        Assert.True(body.GetProperty("changed").GetBoolean());

        Assert.Equal(Enum.Parse<RegulationStatus>(expectedStatus), incident.RegulationStatus);

        decisions.Verify(x => x.AddAsync(
            It.Is<RegulationDecision>(decision =>
                decision.IncidentReferenceId == IncidentReferenceId &&
                decision.RegulatorUserId == RegulatorUserId &&
                decision.Justification == Justification),
            It.IsAny<CancellationToken>()),
            Times.Once);

        incidents.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);

        if (expectedVersion == 2)
        {
            outbox.Verify(x => x.EnqueueAsync(
                $"evt-regulation-{JavaIncidentId}-2",
                "incident.status.changed.v1",
                "incident.status.changed.v1",
                It.Is<string>(payload =>
                    payload.Contains($"\"status\":\"{expectedStatus}\"")),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
        else
        {
            outbox.Verify(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task RegisterDecision_ShouldReturn200_WhenRequestIsRepeated()
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var firstResponse = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        using var json = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync());

        Assert.False(json.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(DecisionId, json.RootElement.GetProperty("decisionId").GetInt64());
        Assert.Equal(RegulatorUserId, json.RootElement.GetProperty("regulatorUserId").GetInt64());

        decisions.Verify(x => x.AddAsync(
            It.IsAny<RegulationDecision>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        incidents.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);

        outbox.Verify(x => x.EnqueueAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("INVALID")]
    [InlineData("0")]
    public async Task RegisterDecision_ShouldReturn400_WhenDecisionTypeIsInvalid(
        string? decisionType)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            decisionType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData("DISPATCH_APPROVED", null)]
    [InlineData("DISPATCH_APPROVED", "INVALID")]
    [InlineData("NO_DISPATCH_REQUIRED", "FIRE_DEPARTMENT")]
    [InlineData("MORE_INFORMATION_REQUIRED", "FIRE_DEPARTMENT")]
    public async Task RegisterDecision_ShouldReturn400_WhenInstitutionIsInvalid(
        string decisionType,
        string? institutionType)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            decisionType,
            institutionType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Curta")]
    public async Task RegisterDecision_ShouldReturn400_WhenJustificationIsInvalid(
        string? justification)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            justification: justification);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Fact]
    public async Task RegisterDecision_ShouldReturn400_WhenJustificationExceedsLimit()
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            justification: new string('A', 1001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-jwt")]
    public async Task RegisterDecision_ShouldReturn401_WhenJwtIsInvalid(string? token)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, token);

        using var response = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("RESPONDER")]
    public async Task RegisterDecision_ShouldReturn403_WhenRoleIsNotAllowed(string role)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(
            factory,
            CreateJwt(role: role));

        using var response = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData("123", 124L)]
    [InlineData("123", null)]
    [InlineData("integration-test@example.com", 0L)]
    [InlineData("0", 123L)]
    public async Task RegisterDecision_ShouldReturn403_WhenIdentityIsInvalid(
        string subject,
        long? userId)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(
            factory,
            CreateJwt(subject, userId));

        using var response = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task RegisterDecision_ShouldReturn400_WhenIncidentIdIsInvalid(long javaIncidentId)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        var path = $"/api/v1/regulation/incidents/{javaIncidentId}/decisions";

        using var response = await PostDecisionAsync(
            client,
            path: path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Fact]
    public async Task RegisterDecision_ShouldReturn404_WhenIncidentDoesNotExist()
    {
        using var factory = CreateFactory(
            null,
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Fact]
    public async Task RegisterDecision_ShouldReturn409_WhenIncidentStatusIsInvalid()
    {
        var incident = CreateIncident();

        incident.AwaitDispatch(DateTime.UtcNow);

        using var factory = CreateFactory(
            incident,
            out var incidents,
            out var decisions,
            out var outbox);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(
            client,
            "NO_DISPATCH_REQUIRED",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(RegulationStatus.AWAITING_DISPATCH, incident.RegulationStatus);

        AssertNoWrites(incidents, decisions, outbox);
    }

    [Fact]
    public async Task RegisterDecision_ShouldReturn409_WhenConcurrencyConflictOccurs()
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var incidents,
            out _,
            out _,
            simulateConcurrencyConflict: true);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await PostDecisionAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        incidents.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterDecision_ShouldUseRegulatorIdFromJwt()
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out _,
            out var decisions,
            out _);

        using var client = CreateClient(factory, CreateJwt());

        using var response = await client.PostAsJsonAsync(
            DecisionPath,
            new
            {
                decisionType = "DISPATCH_APPROVED",
                institutionType = "FIRE_DEPARTMENT",
                justification = Justification,
                regulatorUserId = 9999L
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            RegulatorUserId,
            json.RootElement.GetProperty("regulatorUserId").GetInt64());

        decisions.Verify(x => x.AddAsync(
            It.Is<RegulationDecision>(decision =>
                decision.RegulatorUserId == RegulatorUserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private WebApplicationFactory<Program> CreateFactory(
        IncidentReference? incident,
        out Mock<IIncidentReferenceRepository> incidents,
        out Mock<IRegulationDecisionRepository> decisions,
        out Mock<IIntegrationEventOutbox> outbox,
        bool simulateConcurrencyConflict = false)
    {
        var incidentMock = new Mock<IIncidentReferenceRepository>();
        var decisionMock = new Mock<IRegulationDecisionRepository>();
        var outboxMock = new Mock<IIntegrationEventOutbox>();

        RegulationDecision? latestDecision = null;

        incidentMock.Setup(x => x.GetTrackedByJavaIncidentIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);

        if (simulateConcurrencyConflict)
        {
            incidentMock.Setup(x => x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict."));
        }
        else
        {
            incidentMock.Setup(x => x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        decisionMock.Setup(x => x.GetLatestByIncidentReferenceIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => latestDecision);

        decisionMock.Setup(x => x.AddAsync(
                It.IsAny<RegulationDecision>(),
                It.IsAny<CancellationToken>()))
            .Callback<RegulationDecision, CancellationToken>((decision, _) =>
            {
                typeof(RegulationDecision)
                    .GetProperty(nameof(RegulationDecision.Id))!
                    .SetValue(decision, DecisionId);

                latestDecision = decision;
            })
            .Returns(Task.CompletedTask);

        outboxMock.Setup(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        incidents = incidentMock;
        decisions = decisionMock;
        outbox = outboxMock;

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RabbitMQ:Enabled"] = "false",
                        ["OutboxPublisher:Enabled"] = "false"
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentReferenceRepository>();
                services.RemoveAll<IRegulationDecisionRepository>();
                services.RemoveAll<IIntegrationEventOutbox>();

                services.AddScoped<IIncidentReferenceRepository>(_ => incidentMock.Object);
                services.AddScoped<IRegulationDecisionRepository>(_ => decisionMock.Object);
                services.AddScoped<IIntegrationEventOutbox>(_ => outboxMock.Object);
            });
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        string? token)
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

    private static Task<HttpResponseMessage> PostDecisionAsync(
        HttpClient client,
        string? decisionType = "DISPATCH_APPROVED",
        string? institutionType = "FIRE_DEPARTMENT",
        string? justification = Justification,
        string path = DecisionPath)
    {
        return client.PostAsJsonAsync(
            path,
            new
            {
                decisionType,
                institutionType,
                justification
            });
    }

    private static string CreateJwt(
        string subject = "integration-test@example.com",
        long? userId = RegulatorUserId,
        string role = "REGULATOR")
    {
        var now = DateTimeOffset.UtcNow;

        var payload = new JwtPayload
        {
            ["iss"] = "stormaid-api",
            ["aud"] = "stormaid-services",
            ["sub"] = subject,
            ["iat"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(2).ToUnixTimeSeconds(),
            ["roles"] = new[] { role }
        };

        if (userId.HasValue)
        {
            payload["userId"] = userId.Value;
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(JwtTokenFactory.TestSecret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            new JwtHeader(credentials),
            payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IncidentReference CreateIncident()
    {
        var utcNow = DateTime.UtcNow.AddMinutes(-10);

        var incident = new IncidentReference(
            javaIncidentId: JavaIncidentId,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: utcNow);

        incident.BeginReview(RegulatorUserId, utcNow.AddMinutes(1));
        incident.AdvancePublicStatusVersion(utcNow.AddMinutes(1));

        typeof(IncidentReference)
            .GetProperty(nameof(IncidentReference.Id))!
            .SetValue(incident, IncidentReferenceId);

        return incident;
    }


    private static void AssertNoWrites(
        Mock<IIncidentReferenceRepository> incidents,
        Mock<IRegulationDecisionRepository> decisions,
        Mock<IIntegrationEventOutbox> outbox)
    {
        incidents.Verify(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        decisions.Verify(x => x.AddAsync(
                It.IsAny<RegulationDecision>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        outbox.Verify(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
