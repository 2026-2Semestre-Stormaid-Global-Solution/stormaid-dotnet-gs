
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
