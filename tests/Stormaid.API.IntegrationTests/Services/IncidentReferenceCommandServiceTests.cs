
using System.Text.Json;
using Moq;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

/// <summary>
/// Testes do serviço responsável por iniciar
/// a análise institucional das ocorrências.
/// </summary>
public sealed class IncidentReferenceCommandServiceTests
{
    /// <summary>
    /// Verifica a transição, a auditoria, a geração do evento
    /// e a ordem de registro antes da persistência.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldUpdateIncidentAndEnqueueEvent()
    {
        var incident = CreateIncident();

        var service = CreateService(
            incident,
            out var repository,
            out var outbox
        );

        string? eventPayload = null;
        var eventEnqueued = false;

        outbox.Setup(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<string, string, string, string, CancellationToken>(
                (eventId, eventType, routingKey, payload, _) =>
                {
                    Assert.Equal(
                        "evt-regulation-8-1",
                        eventId
                    );

                    Assert.Equal(
                        "incident.status.changed.v1",
                        eventType
                    );

                    Assert.Equal(
                        "incident.status.changed.v1",
                        routingKey
                    );

                    eventPayload = payload;
                    eventEnqueued = true;
                })
            .Returns(Task.CompletedTask);

        repository.Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ))
            .Callback(() => Assert.True(eventEnqueued))
            .Returns(Task.CompletedTask);

        var result = await service.BeginReviewAsync(
            javaIncidentId: 8,
            regulatorUserId: 1
        );

        // 1 - Validar o resultado da análise.

        Assert.True(result.Changed);

        Assert.Equal(
            8L,
            result.JavaIncidentId
        );

        Assert.Equal(
            RegulationStatus.UNDER_REVIEW,
            result.RegulationStatus
        );

        Assert.Equal(
            1L,
            result.PublicStatusVersion
        );

        Assert.Equal(
            2L,
            incident.Version
        );

        // 2 - Validar a auditoria do regulador.

        Assert.Equal(
            1L,
            incident.ReviewedByRegulatorUserId
        );

        Assert.NotNull(incident.ReviewStartedAt);

        Assert.Equal(
            DateTimeKind.Utc,
            incident.ReviewStartedAt.Value.Kind
        );

        Assert.Equal(
            RegulationStatus.UNDER_REVIEW,
            incident.RegulationStatus
        );

        // 3 - Validar o conteúdo do evento.

        Assert.NotNull(eventPayload);

        using var document = JsonDocument.Parse(eventPayload);

        var payload = document.RootElement;

        Assert.Equal(
            "incident.status.changed.v1",
            payload.GetProperty("eventType").GetString()
        );

        Assert.Equal(
            "evt-regulation-8-1",
            payload.GetProperty("eventId").GetString()
        );

        Assert.Equal(
            8L,
            payload.GetProperty("incidentId").GetInt64()
        );

        Assert.Equal(
            "UNDER_REVIEW",
            payload.GetProperty("status").GetString()
        );

        Assert.Equal(
            1L,
            payload.GetProperty("version").GetInt64()
        );

        Assert.True(
            payload.TryGetProperty("changedAt", out _)
        );

        // 4 - Verificar a persistência e a Outbox.

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
    /// Verifica que repetir a análise não gera outro evento,
    /// não altera a versão e preserva o regulador original.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldBeIdempotent()
    {
        var incident = CreateIncident();

        var service = CreateService(
            incident,
            out var repository,
            out var outbox
        );

        // 1 - Primeiro regulador inicia a análise.

        var first = await service.BeginReviewAsync(
            javaIncidentId: 8,
            regulatorUserId: 901
        );

        var originalReviewStartedAt =
            incident.ReviewStartedAt;

        var originalVersion = incident.Version;

        // 2 - Outro regulador tenta iniciar a mesma análise.

        var second = await service.BeginReviewAsync(
            javaIncidentId: 8,
            regulatorUserId: 902
        );

        // 3 - Verificar a idempotência.

        Assert.True(first.Changed);
        Assert.False(second.Changed);

        Assert.Equal(
            RegulationStatus.UNDER_REVIEW,
            second.RegulationStatus
        );

        Assert.Equal(
            first.PublicStatusVersion,
            second.PublicStatusVersion
        );

        Assert.Equal(
            originalVersion,
            incident.Version
        );

        // 4 - Preservar a identidade do primeiro regulador.

        Assert.Equal(
            901L,
            incident.ReviewedByRegulatorUserId
        );

        Assert.NotNull(originalReviewStartedAt);

        Assert.Equal(
            originalReviewStartedAt,
            incident.ReviewStartedAt
        );

        Assert.Equal(
            DateTimeKind.Utc,
            incident.ReviewStartedAt!.Value.Kind
        );

        // 5 - Não permitir duplicação da persistência
        // nem do evento de integração.

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
    /// Verifica o comportamento quando
    /// a ocorrência não existe no repositório.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldRejectMissingIncident()
    {
        var service = CreateService(
            null,
            out var repository,
            out var outbox
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.BeginReviewAsync(999, 1)
        );

        AssertNoWrites(repository, outbox);
    }

    /// <summary>
    /// Verifica a rejeição de uma ocorrência
    /// que já avançou para despacho.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldRejectInvalidTransition()
    {
        var incident = CreateIncident();

        incident.BeginReview(
            DateTime.UtcNow.AddMinutes(-2)
        );

        incident.AwaitDispatch(
            DateTime.UtcNow.AddMinutes(-1)
        );

        var service = CreateService(
            incident,
            out var repository,
            out var outbox
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BeginReviewAsync(8, 1)
        );

        Assert.Equal(
            RegulationStatus.AWAITING_DISPATCH,
            incident.RegulationStatus
        );

        AssertNoWrites(repository, outbox);
    }

    /// <summary>
    /// Verifica a rejeição dos identificadores
    /// inválidos antes de consultar o repositório.
    /// </summary>
    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(-1L, 1L)]
    [InlineData(8L, 0L)]
    [InlineData(8L, -1L)]
    public async Task BeginReviewAsync_ShouldRejectInvalidIds(
        long javaIncidentId,
        long regulatorUserId)
    {
        var service = CreateService(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.BeginReviewAsync(
                javaIncidentId,
                regulatorUserId
            )
        );

        repository.VerifyNoOtherCalls();
        outbox.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifica que uma falha ao registrar
    /// o evento impede a chamada de persistência.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldNotSave_WhenOutboxFails()
    {
        var service = CreateService(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        outbox.Setup(x => x.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.FromException(
                new TimeoutException("Outbox unavailable.")
            ));

        await Assert.ThrowsAsync<TimeoutException>(
            () => service.BeginReviewAsync(8, 1)
        );

        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    /// <summary>
    /// Verifica que uma falha de persistência
    /// é propagada para a camada responsável.
    /// </summary>
    [Fact]
    public async Task BeginReviewAsync_ShouldPropagateSaveFailure()
    {
        var service = CreateService(
            CreateIncident(),
            out var repository,
            out var outbox
        );

        repository.Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.FromException(
                new TimeoutException("Oracle unavailable.")
            ));

        await Assert.ThrowsAsync<TimeoutException>(
            () => service.BeginReviewAsync(8, 1)
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
    /// Configura os repositórios simulados
    /// utilizados pelos testes do serviço.
    /// </summary>
    private static IncidentReferenceCommandService CreateService(
        IncidentReference? incident,
        out Mock<IIncidentReferenceRepository> repository,
        out Mock<IIntegrationEventOutbox> outbox)
    {
        repository = new Mock<IIncidentReferenceRepository>();
        outbox = new Mock<IIntegrationEventOutbox>();

        repository.Setup(
                x => x.GetTrackedByJavaIncidentIdAsync(
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(incident);

        repository.Setup(
                x => x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        outbox.Setup(
                x => x.EnqueueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        return new IncidentReferenceCommandService(
            repository.Object,
            outbox.Object
        );
    }

    /// <summary>
    /// Confirma que nenhuma escrita foi solicitada.
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
    /// Cria uma ocorrência válida para os testes.
    /// </summary>
    private static IncidentReference CreateIncident()
    {
        return new IncidentReference(
            javaIncidentId: 8,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: DateTime.UtcNow.AddMinutes(-10)
        );
    }
}
