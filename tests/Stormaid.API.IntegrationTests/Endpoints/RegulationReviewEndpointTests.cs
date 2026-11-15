
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

/// <summary>
/// Testes HTTP do endpoint responsável por iniciar
/// a análise institucional das ocorrências.
/// </summary>
public sealed class RegulationReviewEndpointTests
    : IClassFixture<StormaidWebApplicationFactory>
{
    private const string ReviewPath =
        "/api/v1/regulation/incidents/8/review";

    private readonly StormaidWebApplicationFactory _factory;

    /// <summary>
    /// Inicializa os testes utilizando a factory
    /// compartilhada da API Storm Aid.
    /// </summary>
    public RegulationReviewEndpointTests(
        StormaidWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifica que um regulador autenticado consegue
    /// iniciar a análise de uma ocorrência pendente.
    /// </summary>
    [Fact]
    public async Task BeginReview_ShouldReturn200_WhenIncidentIsPending()
    {
        var incident = CreateIncident();

        using var factory = CreateFactory(
            incident,
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt()
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        var body = json.RootElement;

        Assert.Equal(
            8L,
            body.GetProperty("javaIncidentId").GetInt64()
        );

        Assert.Equal(
            "UNDER_REVIEW",
            body.GetProperty("regulationStatus").GetString()
        );

        Assert.Equal(
            901L,
            body.GetProperty(
                "reviewedByRegulatorUserId"
            ).GetInt64()
        );

        Assert.Equal(
            RegulationStatus.UNDER_REVIEW,
            incident.RegulationStatus
        );

        Assert.Equal(
            901L,
            incident.ReviewedByRegulatorUserId
        );

        Assert.NotNull(incident.ReviewStartedAt);

        Assert.Equal(
            DateTimeKind.Utc,
            incident.ReviewStartedAt.Value.Kind
        );

        Assert.Equal(
            1L,
            incident.PublicStatusVersion
        );

        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        outbox.Verify(
            x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    /// <summary>
    /// Verifica que uma segunda solicitação
    /// preserva a autoria e não duplica a Outbox.
    /// </summary>
    [Fact]
    public async Task BeginReview_ShouldPreserveOriginalRegulator()
    {
        var incident = CreateIncident();

        using var factory = CreateFactory(
            incident,
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt(subject: "901", userId: 901)
        );

        using var firstResponse = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode
        );

        var originalReviewStartedAt =
            incident.ReviewStartedAt;

        var originalVersion =
            incident.PublicStatusVersion;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateJwt(subject: "902", userId: 902)
            );

        using var secondResponse = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode
        );

        using var json = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync()
        );

        Assert.Equal(
            901L,
            json.RootElement.GetProperty(
                "reviewedByRegulatorUserId"
            ).GetInt64()
        );

        Assert.Equal(
            originalReviewStartedAt,
            incident.ReviewStartedAt
        );

        Assert.Equal(
            originalVersion,
            incident.PublicStatusVersion
        );

        Assert.Equal(
            901L,
            incident.ReviewedByRegulatorUserId
        );

        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        outbox.Verify(
            x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    /// <summary>
    /// Verifica a rejeição de requisições
    /// sem JWT ou com token malformado.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-valid-jwt")]
    public async Task BeginReview_ShouldReturn401_WhenJwtIsInvalid(
        string? token)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            token
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode
        );

        AssertNoWrites(repository, outbox);
        AssertNoRepositoryReads(repository);
    }

    /// <summary>
    /// Verifica que usuários sem a função
    /// REGULATOR não conseguem iniciar análises.
    /// </summary>
    [Theory]
    [InlineData("RESPONDER")]
    [InlineData("CITIZEN")]
    public async Task BeginReview_ShouldReturn403_WhenRoleIsNotAllowed(
        string role)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt(role: role)
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode
        );

        AssertNoWrites(repository, outbox);
        AssertNoRepositoryReads(repository);
    }

    /// <summary>
    /// Verifica a rejeição de claims institucionais
    /// inconsistentes, inválidas ou ausentes.
    /// </summary>
    [Theory]
    [InlineData("901", 902L)]
    [InlineData("integration-test@example.com", 901L)]
    [InlineData("0", 901L)]
    [InlineData("901", null)]
    public async Task BeginReview_ShouldReturn403_WhenIdentityIsInvalid(
        string subject,
        long? userId)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt(
                subject: subject,
                userId: userId
            )
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode
        );

        AssertNoWrites(repository, outbox);
        AssertNoRepositoryReads(repository);
    }

    /// <summary>
    /// Verifica que identificadores inválidos
    /// são rejeitados antes da consulta à ocorrência.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task BeginReview_ShouldReturn400_WhenIncidentIdIsInvalid(
        long javaIncidentId)
    {
        using var factory = CreateFactory(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt()
        );

        var path =
            $"/api/v1/regulation/incidents/{javaIncidentId}/review";

        using var response = await client.PostAsync(
            path,
            null
        );

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        AssertNoWrites(repository, outbox);
        AssertNoRepositoryReads(repository);
    }

    /// <summary>
    /// Verifica que uma ocorrência inexistente
    /// resulta em HTTP 404.
    /// </summary>
    [Fact]
    public async Task BeginReview_ShouldReturn404_WhenIncidentDoesNotExist()
    {
        using var factory = CreateFactory(
            null,
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt()
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode
        );

        AssertNoWrites(repository, outbox);
    }

    /// <summary>
    /// Verifica que uma ocorrência que já avançou
    /// para despacho não pode reiniciar a análise.
    /// </summary>
    [Fact]
    public async Task BeginReview_ShouldReturn409_WhenTransitionIsInvalid()
    {
        var incident = CreateIncident();

        incident.BeginReview(
            901,
            DateTime.UtcNow.AddMinutes(-2)
        );

        incident.AwaitDispatch(
            DateTime.UtcNow.AddMinutes(-1)
        );

        using var factory = CreateFactory(
            incident,
            out var repository,
            out var outbox
        );

        using var client = CreateClient(
            factory,
            CreateJwt()
        );

        using var response = await client.PostAsync(
            ReviewPath,
            null
        );

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode
        );

        Assert.Equal(
            RegulationStatus.AWAITING_DISPATCH,
            incident.RegulationStatus
        );

        AssertNoWrites(repository, outbox);
    }

    /// <summary>
    /// Cria uma instância isolada da aplicação,
    /// substituindo persistência e Outbox por mocks.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(
        IncidentReference? incident,
        out Mock<IIncidentReferenceRepository> repository,
        out Mock<IIntegrationEventOutbox> outbox)
    {
        var repositoryMock =
            new Mock<IIncidentReferenceRepository>();

        var outboxMock =
            new Mock<IIntegrationEventOutbox>();

        repositoryMock
            .Setup(x => x.GetTrackedByJavaIncidentIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(incident);

        repositoryMock
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        outboxMock
            .Setup(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        repository = repositoryMock;
        outbox = outboxMock;

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["RabbitMQ:Enabled"] = "false",
                            ["OutboxPublisher:Enabled"] = "false"
                        }
                    );
                }
            );

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<
                    IIncidentReferenceRepository>();

                services.RemoveAll<
                    IIntegrationEventOutbox>();

                services.AddScoped<
                    IIncidentReferenceRepository>(
                    _ => repositoryMock.Object
                );

                services.AddScoped<
                    IIntegrationEventOutbox>(
                    _ => outboxMock.Object
                );
            });
        });
    }

    /// <summary>
    /// Cria um cliente HTTP com HTTPS e JWT opcional.
    /// </summary>
    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        string? token)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            }
        );

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );
        }

        return client;
    }

    /// <summary>
    /// Cria um JWT com o contrato institucional
    /// esperado pela API .NET e emitido pela API Java.
    /// </summary>
    private static string CreateJwt(
        string subject = "901",
        long? userId = 901,
        string role = "REGULATOR")
    {
        var now = DateTimeOffset.UtcNow;

        var payload = new JwtPayload
        {
            ["iss"] = "stormaid-api",
            ["aud"] = "stormaid-services",
            ["sub"] = subject,
            ["iat"] = now.AddMinutes(-1)
                .ToUnixTimeSeconds(),
            ["nbf"] = now.AddMinutes(-1)
                .ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(2)
                .ToUnixTimeSeconds(),
            ["roles"] = new[] { role }
        };

        if (userId.HasValue)
        {
            payload["userId"] = userId.Value;
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                JwtTokenFactory.TestSecret
            )
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var header = new JwtHeader(credentials);

        var token = new JwtSecurityToken(
            header,
            payload
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    /// <summary>
    /// Confirma que nenhuma gravação foi solicitada
    /// ao repositório nem à Outbox.
    /// </summary>
    private static void AssertNoWrites(
        Mock<IIncidentReferenceRepository> repository,
        Mock<IIntegrationEventOutbox> outbox)
    {
        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );

        outbox.Verify(
            x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    /// <summary>
    /// Confirma que requisições recusadas
    /// não chegam à consulta do repositório.
    /// </summary>
    private static void AssertNoRepositoryReads(
        Mock<IIncidentReferenceRepository> repository)
    {
        repository.Verify(
            x => x.GetTrackedByJavaIncidentIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    /// <summary>
    /// Cria uma ocorrência pendente e válida
    /// para os cenários HTTP dos testes.
    /// </summary>
    private static IncidentReference CreateIncident()
    {
        return new IncidentReference(
            javaIncidentId: 8,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory:
                EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: DateTime.UtcNow.AddMinutes(-10)
        );
    }
}
