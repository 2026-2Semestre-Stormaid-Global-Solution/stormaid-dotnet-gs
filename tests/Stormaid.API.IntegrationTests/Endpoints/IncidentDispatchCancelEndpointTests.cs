using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Stormaid.API.IntegrationTests.Factories;
using Stormaid.API.IntegrationTests.Helpers;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;
using Stormaid.Domain.Entities;
using System.Text.Json;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Endpoints;

public sealed class IncidentDispatchCancelEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const string Endpoint = "/api/v1/dispatches/90/cancel";

    private readonly StormaidWebApplicationFactory _factory;

    public IncidentDispatchCancelEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cancel_ShouldReturn401_WhenTokenIsMissing()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason = "Cancelamento institucional." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("RESPONDER")]
    public async Task Cancel_ShouldReturn403_WhenRoleIsNotRegulator(
        string role)
    {
        using var client = CreateClient(role);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason = "Cancelamento institucional." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ShouldReturn400_WhenDispatchIdIsInvalid()
    {
        using var client = CreateClient("REGULATOR");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/dispatches/0/cancel",
            new { reason = "Identificador inválido." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancel_ShouldReturn400_WhenReasonIsInvalid(
        string reason)
    {
        using var client = CreateClient("REGULATOR");

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ShouldReturn404_WhenDispatchDoesNotExist()
    {
        var dispatches = new Mock<IIncidentDispatchRepository>();

        dispatches.Setup(repository =>
                repository.GetTrackedByIdAsync(
                    90,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((IncidentDispatch?)null);

        var transaction = new Mock<ITransactionExecutor>();

        transaction.Setup(executor =>
                executor.ExecuteAsync(
                    It.IsAny<Func<CancellationToken,
                        Task<IncidentDispatchResponse>>>(),
                    It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken,
                    Task<IncidentDispatchResponse>> operation,
                CancellationToken token) => operation(token));

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentDispatchRepository>();
                services.RemoveAll<ITransactionExecutor>();

                services.AddScoped(_ => dispatches.Object);
                services.AddScoped(_ => transaction.Object);
            });
        });

        using var client = CreateClient("REGULATOR", factory);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason = "Cancelamento institucional." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    
    [Fact]
    public async Task Cancel_ShouldReturn200_WhenDispatchCanBeCancelled()
    {
        var scenario = CreateCancelScenarioFactory(false);
        using var factory = scenario.Factory;
        using var client = CreateClient("REGULATOR", factory);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason = "Cancelamento institucional." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "CANCELLED",
            json.RootElement.GetProperty("status").GetString());

        scenario.Outbox.Verify(outbox => outbox.EnqueueAsync(
            It.IsAny<string>(),
            "incident.status.changed.v1",
            "incident.status.changed.v1",
            It.Is<string>(payload =>
                payload.Contains("\"status\":\"CANCELLED\"")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldReturn409_WhenDispatchIsResolved()
    {
        var scenario = CreateCancelScenarioFactory(true);
        using var factory = scenario.Factory;
        using var client = CreateClient("REGULATOR", factory);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { reason = "Tentativa de cancelamento." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        scenario.Outbox.Verify(outbox => outbox.EnqueueAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private (
        WebApplicationFactory<Program> Factory,
        Mock<IIntegrationEventOutbox> Outbox
    ) CreateCancelScenarioFactory(bool alreadyResolved)
    {
        var now = DateTime.UtcNow.AddMinutes(-10);

        var incident = new IncidentReference(
            900010,
            1,
            RecipientType.SELF,
            ChannelType.TEXT_INPUT,
            EmergencyCategory.CLIMATE_DISASTER,
            -23.5505m,
            -46.6333m,
            "Ocorrência de teste",
            null,
            now);

        incident.BeginReview(123, now.AddMinutes(1));
        incident.AdvancePublicStatusVersion(now.AddMinutes(1));

        incident.AwaitDispatch(now.AddMinutes(2));
        incident.AdvancePublicStatusVersion(now.AddMinutes(2));

        incident.MarkDispatched(now.AddMinutes(3));
        incident.AdvancePublicStatusVersion(now.AddMinutes(3));

        typeof(IncidentReference)
            .GetProperty(nameof(IncidentReference.Id))!
            .SetValue(incident, 50L);

        var unit = new RescueUnit(
            "BOM-001",
            "Equipe de Bombeiros",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            4,
            true,
            now);

        typeof(RescueUnit)
            .GetProperty(nameof(RescueUnit.Id))!
            .SetValue(unit, 70L);

        unit.MarkAsBusy(now.AddMinutes(4));

        var dispatch = new IncidentDispatch(
            50,
            60,
            70,
            123,
            "Despacho de teste.",
            now.AddMinutes(4));

        typeof(IncidentDispatch)
            .GetProperty(nameof(IncidentDispatch.Id))!
            .SetValue(dispatch, 90L);

        dispatch.Accept(20, now.AddMinutes(5));

        if (alreadyResolved)
        {
            dispatch.StartRoute(now.AddMinutes(6));
            dispatch.Arrive(now.AddMinutes(7));
            dispatch.Resolve(now.AddMinutes(8));
        }

        var dispatches = new Mock<IIncidentDispatchRepository>();
        var incidents = new Mock<IIncidentReferenceRepository>();
        var units = new Mock<IRescueUnitRepository>();
        var outbox = new Mock<IIntegrationEventOutbox>();
        var transaction = new Mock<ITransactionExecutor>();

        dispatches.Setup(repository =>
                repository.GetTrackedByIdAsync(
                    90,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispatch);

        incidents.Setup(repository =>
                repository.GetTrackedByIdAsync(
                    50,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);

        units.Setup(repository =>
                repository.GetTrackedByIdAsync(
                    70,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);

        outbox.Setup(repository => repository.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        transaction.Setup(executor =>
                executor.ExecuteAsync(
                    It.IsAny<Func<CancellationToken,
                        Task<IncidentDispatchResponse>>>(),
                    It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken,
                    Task<IncidentDispatchResponse>> operation,
                CancellationToken token) => operation(token));

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIncidentDispatchRepository>();
                services.RemoveAll<IIncidentReferenceRepository>();
                services.RemoveAll<IRescueUnitRepository>();
                services.RemoveAll<IIntegrationEventOutbox>();
                services.RemoveAll<ITransactionExecutor>();

                services.AddScoped<IIncidentDispatchRepository>(
                    _ => dispatches.Object);

                services.AddScoped<IIncidentReferenceRepository>(
                    _ => incidents.Object);

                services.AddScoped<IRescueUnitRepository>(
                    _ => units.Object);

                services.AddScoped<IIntegrationEventOutbox>(
                    _ => outbox.Object);

                services.AddScoped<ITransactionExecutor>(
                    _ => transaction.Object);
            });
        });

        return (factory, outbox);
    }


    private HttpClient CreateClient(
        string? role = null,
        WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(
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
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
