using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Stormaid.API.Hateoas;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Common;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Endpoints;

public sealed class IncidentDispatchHateoasEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const long DispatchId = 90;
    private const long IncidentId = 50;
    private const long DecisionId = 60;
    private const long RescueUnitId = 70;
    private const long JavaIncidentId = 900010;
    private const long RegulatorId = 123;

    private readonly StormaidWebApplicationFactory _factory;

    public IncidentDispatchHateoasEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(DispatchStatus.PENDING_ACCEPTANCE)]
    [InlineData(DispatchStatus.ACCEPTED)]
    [InlineData(DispatchStatus.EN_ROUTE)]
    [InlineData(DispatchStatus.ON_SITE)]
    public void Build_ShouldOfferCancelToRegulator_WhenStatusAllowsIt(
        DispatchStatus status)
    {
        var result = IncidentDispatchHateoasLinks.Build(
            CreateResponse(status),
            CreatePrincipal("REGULATOR"));

        Assert.Contains(result.Links, link =>
            link.Rel == "cancel" &&
            link.Href == $"/api/v1/dispatches/{DispatchId}/cancel" &&
            link.Method == "POST");
    }

    [Theory]
    [InlineData(DispatchStatus.CANCELLED)]
    [InlineData(DispatchStatus.RESOLVED)]
    public void Build_ShouldNotOfferCancel_WhenDispatchIsFinalized(
        DispatchStatus status)
    {
        var result = IncidentDispatchHateoasLinks.Build(
            CreateResponse(status),
            CreatePrincipal("REGULATOR"));

        Assert.DoesNotContain(result.Links, link => link.Rel == "cancel");
    }

    [Fact]
    public void Build_ShouldNotOfferCancelToResponder()
    {
        var result = IncidentDispatchHateoasLinks.Build(
            CreateResponse(DispatchStatus.ACCEPTED),
            CreatePrincipal("RESPONDER"));

        Assert.DoesNotContain(result.Links, link => link.Rel == "cancel");
    }

    [Fact]
    public void Build_ShouldIncludeValidRescueUnitLink()
    {
        var result = IncidentDispatchHateoasLinks.Build(
            CreateResponse(DispatchStatus.ACCEPTED),
            CreatePrincipal("REGULATOR"));

        Assert.Contains(result.Links, link =>
            link.Rel == "rescue-unit" &&
            link.Href == $"/api/v1/rescue-units/{RescueUnitId}" &&
            link.Method == "GET");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?includeLinks=false")]
    public async Task Create_ShouldPreserveOriginalJson_WhenLinksAreNotRequested(
        string query)
    {
        using var factory = CreateFactory(forCancellation: false);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/dispatches" + query,
            new
            {
                javaIncidentId = JavaIncidentId,
                rescueUnitId = RescueUnitId,
                notes = "Despacho de teste HATEOAS."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(DispatchId,
            document.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("PENDING_ACCEPTANCE",
            document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.TryGetProperty("data", out _));
        Assert.False(document.RootElement.TryGetProperty("links", out _));
    }

    [Fact]
    public async Task Create_ShouldReturnHateoas_WhenLinksAreRequested()
    {
        using var factory = CreateFactory(forCancellation: false);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/dispatches?includeLinks=true",
            new
            {
                javaIncidentId = JavaIncidentId,
                rescueUnitId = RescueUnitId,
                notes = "Despacho de teste HATEOAS."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var data = document.RootElement.GetProperty("data");
        var links = document.RootElement.GetProperty("links");

        Assert.Equal(DispatchId, data.GetProperty("id").GetInt64());
        Assert.Equal("PENDING_ACCEPTANCE",
            data.GetProperty("status").GetString());
        Assert.Contains(links.EnumerateArray(), link =>
            link.GetProperty("rel").GetString() == "cancel" &&
            link.GetProperty("href").GetString() ==
                $"/api/v1/dispatches/{DispatchId}/cancel" &&
            link.GetProperty("method").GetString() == "POST");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?includeLinks=false")]
    public async Task Cancel_ShouldPreserveOriginalJson_WhenLinksAreNotRequested(
        string query)
    {
        using var factory = CreateFactory(forCancellation: true);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/dispatches/{DispatchId}/cancel" + query,
            new { reason = "Cancelamento institucional de teste." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal("CANCELLED",
            document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.TryGetProperty("data", out _));
        Assert.False(document.RootElement.TryGetProperty("links", out _));
    }

    [Fact]
    public async Task Cancel_ShouldReturnHateoasWithoutCancelLink()
    {
        using var factory = CreateFactory(forCancellation: true);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/dispatches/{DispatchId}/cancel?includeLinks=true",
            new { reason = "Cancelamento institucional de teste." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var data = document.RootElement.GetProperty("data");
        var links = document.RootElement.GetProperty("links");

        Assert.Equal("CANCELLED", data.GetProperty("status").GetString());
        Assert.DoesNotContain(links.EnumerateArray(), link =>
            link.GetProperty("rel").GetString() == "cancel");
        Assert.Contains(links.EnumerateArray(), link =>
            link.GetProperty("rel").GetString() == "rescue-unit" &&
            link.GetProperty("href").GetString() ==
                $"/api/v1/rescue-units/{RescueUnitId}");
    }

    private WebApplicationFactory<Program> CreateFactory(bool forCancellation)
    {
        var now = DateTime.UtcNow.AddMinutes(-15);

        var incident = new IncidentReference(
            JavaIncidentId,
            1,
            RecipientType.SELF,
            ChannelType.TEXT_INPUT,
            EmergencyCategory.CLIMATE_DISASTER,
            -23.5505m,
            -46.6333m,
            "Ocorrência de teste HATEOAS",
            null,
            now);

        incident.BeginReview(RegulatorId, now.AddMinutes(1));
        incident.AdvancePublicStatusVersion(now.AddMinutes(1));
        incident.AwaitDispatch(now.AddMinutes(2));
        incident.AdvancePublicStatusVersion(now.AddMinutes(2));
        SetId(incident, IncidentId);

        var decision = new RegulationDecision(
            IncidentId,
            RegulatorId,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            "Despacho aprovado para teste HATEOAS.",
            now.AddMinutes(3));
        SetId(decision, DecisionId);

        var unit = new RescueUnit(
            "BOM-070",
            "Unidade de teste HATEOAS",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            4,
            true,
            now);
        SetId(unit, RescueUnitId);

        IncidentDispatch? existingDispatch = null;

        if (forCancellation)
        {
            incident.MarkDispatched(now.AddMinutes(4));
            incident.AdvancePublicStatusVersion(now.AddMinutes(4));
            unit.MarkAsBusy(now.AddMinutes(4));

            existingDispatch = new IncidentDispatch(
                IncidentId,
                DecisionId,
                RescueUnitId,
                RegulatorId,
                "Despacho de teste.",
                now.AddMinutes(4));
            SetId(existingDispatch, DispatchId);
            existingDispatch.Accept(20, now.AddMinutes(5));
        }

        var incidents = new Mock<IIncidentReferenceRepository>();
        incidents.Setup(repo => repo.GetTrackedByJavaIncidentIdAsync(
                JavaIncidentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);
        incidents.Setup(repo => repo.GetTrackedByIdAsync(
                IncidentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);

        var decisions = new Mock<IRegulationDecisionRepository>();
        decisions.Setup(repo => repo.GetLatestByIncidentReferenceIdAsync(
                IncidentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);

        var units = new Mock<IRescueUnitRepository>();
        units.Setup(repo => repo.GetTrackedByIdAsync(
                RescueUnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);

        var dispatches = new Mock<IIncidentDispatchRepository>();
        dispatches.Setup(repo => repo.GetTrackedByIdAsync(
                DispatchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDispatch);
        dispatches.Setup(repo => repo.GetActiveByIncidentReferenceIdAsync(
                IncidentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IncidentDispatch?)null);
        dispatches.Setup(repo => repo.AddAsync(
                It.IsAny<IncidentDispatch>(), It.IsAny<CancellationToken>()))
            .Callback<IncidentDispatch, CancellationToken>(
                (dispatch, _) => SetId(dispatch, DispatchId))
            .Returns(Task.CompletedTask);
        dispatches.Setup(repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dispatches.Setup(repo => repo.AddHistoryAsync(
                It.IsAny<DispatchStatusHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var outbox = new Mock<IIntegrationEventOutbox>();
        outbox.Setup(repo => repo.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var transaction = new Mock<ITransactionExecutor>();
        transaction.Setup(executor => executor.ExecuteAsync(
                It.IsAny<Func<CancellationToken,
                    Task<IncidentDispatchResponse>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task<IncidentDispatchResponse>> operation,
                CancellationToken token) => operation(token));

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentReferenceRepository>();
                services.RemoveAll<IRegulationDecisionRepository>();
                services.RemoveAll<IRescueUnitRepository>();
                services.RemoveAll<IIncidentDispatchRepository>();
                services.RemoveAll<IIntegrationEventOutbox>();
                services.RemoveAll<ITransactionExecutor>();

                services.AddScoped<IIncidentReferenceRepository>(_ => incidents.Object);
                services.AddScoped<IRegulationDecisionRepository>(_ => decisions.Object);
                services.AddScoped<IRescueUnitRepository>(_ => units.Object);
                services.AddScoped<IIncidentDispatchRepository>(_ => dispatches.Object);
                services.AddScoped<IIntegrationEventOutbox>(_ => outbox.Object);
                services.AddScoped<ITransactionExecutor>(_ => transaction.Object);
            });
        });
    }

    private static IncidentDispatchResponse CreateResponse(DispatchStatus status)
    {
        return new IncidentDispatchResponse(
            DispatchId,
            IncidentId,
            DecisionId,
            RescueUnitId,
            status,
            RegulatorId,
            "Despacho de teste.",
            DateTime.UtcNow,
            1);
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, role) },
            authenticationType: "Test"));
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var token = JwtTokenFactory.CreateToken(
            JwtTokenFactory.TestSecret,
            role: "REGULATOR");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static void SetId<T>(T entity, long id) where T : Entity
    {
        typeof(T).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
    }
}
