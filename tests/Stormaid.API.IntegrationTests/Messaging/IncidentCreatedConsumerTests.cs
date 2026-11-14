using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Stormaid.API.Messaging;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;
using Stormaid.Infrastructure.Messaging;

namespace Stormaid.API.IntegrationTests.Messaging;

public sealed class IncidentCreatedConsumerTests
{
    private const string ValidPayload = """
        {
          "incidentId": 150,
          "requesterUserId": 25,
          "recipientType": "SELF",
          "channelType": "TEXT_INPUT",
          "emergencyCategory": "CLIMATE_DISASTER",
          "publicStatus": "RECEIVED",
          "location": {
            "source": "CURRENT_LOCATION",
            "latitude": -23.5505,
            "longitude": -46.6333,
            "addressText": "São Paulo - SP"
          },
          "createdAt": "2026-11-14T19:38:41Z"
        }
        """;

    [Fact]
    public async Task ShouldAcknowledgeOnlyAfterPersistence()
    {
        var repository = new FakeIncidentReferenceRepository();

        using var services = CreateServices(repository);

        var consumer = CreateConsumer(services);
        var channel = new Mock<IChannel>();

        var persistedBeforeAck = false;

        channel
            .Setup(x => x.BasicAckAsync(
                10UL,
                false,
                It.IsAny<CancellationToken>()
            ))
            .Returns(() =>
            {
                persistedBeforeAck = repository.PersistedIncident is not null;

                return ValueTask.CompletedTask;
            });

        var payload = Encoding.UTF8.GetBytes(ValidPayload);

        await consumer.ProcessDeliveryAsync(
            channel.Object,
            payload,
            10UL,
            CancellationToken.None
        );

        Assert.True(persistedBeforeAck);
        Assert.NotNull(repository.PersistedIncident);
        Assert.Equal(
            150L,
            repository.PersistedIncident.JavaIncidentId
        );

        channel.Verify(
            x => x.BasicAckAsync(
                10UL,
                false,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        channel.Verify(
            x => x.BasicNackAsync(
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task ShouldRejectInvalidJsonWithoutPersistence()
    {
        var repository = new FakeIncidentReferenceRepository();

        using var services = CreateServices(repository);

        var consumer = CreateConsumer(services);
        var channel = new Mock<IChannel>();

        channel
            .Setup(x => x.BasicNackAsync(
                20UL,
                false,
                false,
                It.IsAny<CancellationToken>()
            ))
            .Returns(ValueTask.CompletedTask);

        var payload = Encoding.UTF8.GetBytes(
            """{"incidentId":"""
        );

        await consumer.ProcessDeliveryAsync(
            channel.Object,
            payload,
            20UL,
            CancellationToken.None
        );

        Assert.Null(repository.PersistedIncident);

        channel.Verify(
            x => x.BasicNackAsync(
                20UL,
                false,
                false,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        channel.Verify(
            x => x.BasicAckAsync(
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task ShouldRetryAfterTemporaryDatabaseFailure()
    {
        var repository = new FakeIncidentReferenceRepository
        {
            FailuresRemaining = 1
        };

        using var services = CreateServices(repository);

        var consumer = CreateConsumer(services);
        var channel = new Mock<IChannel>();

        channel
            .Setup(x => x.BasicAckAsync(
                30UL,
                false,
                It.IsAny<CancellationToken>()
            ))
            .Returns(ValueTask.CompletedTask);

        var payload = Encoding.UTF8.GetBytes(ValidPayload);

        await consumer.ProcessDeliveryAsync(
            channel.Object,
            payload,
            30UL,
            CancellationToken.None
        );

        Assert.Equal(2, repository.InsertAttempts);
        Assert.NotNull(repository.PersistedIncident);

        channel.Verify(
            x => x.BasicAckAsync(
                30UL,
                false,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        channel.Verify(
            x => x.BasicNackAsync(
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task ShouldRejectAfterThreeDatabaseFailures()
    {
        var repository = new FakeIncidentReferenceRepository
        {
            FailuresRemaining = 3
        };

        using var services = CreateServices(repository);

        var consumer = CreateConsumer(services);
        var channel = new Mock<IChannel>();

        channel
            .Setup(x => x.BasicNackAsync(
                40UL,
                false,
                false,
                It.IsAny<CancellationToken>()
            ))
            .Returns(ValueTask.CompletedTask);

        var payload = Encoding.UTF8.GetBytes(ValidPayload);

        await consumer.ProcessDeliveryAsync(
            channel.Object,
            payload,
            40UL,
            CancellationToken.None
        );

        Assert.Equal(3, repository.InsertAttempts);
        Assert.Null(repository.PersistedIncident);

        channel.Verify(
            x => x.BasicNackAsync(
                40UL,
                false,
                false,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        channel.Verify(
            x => x.BasicAckAsync(
                It.IsAny<ulong>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    private static ServiceProvider CreateServices(
        FakeIncidentReferenceRepository repository
    )
    {
        return new ServiceCollection()
            .AddSingleton<IIncidentReferenceRepository>(repository)
            .AddScoped<IncidentCreatedEventService>()
            .BuildServiceProvider();
    }

    private static IncidentCreatedConsumer CreateConsumer(
        IServiceProvider services
    )
    {
        return new IncidentCreatedConsumer(
            Options.Create(new RabbitMqOptions()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<IncidentCreatedConsumer>.Instance
        );
    }

    private sealed class FakeIncidentReferenceRepository
        : IIncidentReferenceRepository
    {
        public IncidentReference? PersistedIncident { get; private set; }

        public int FailuresRemaining { get; set; }

        public int InsertAttempts { get; private set; }

        public Task<IncidentReference?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default
        )
        {
            var existing = PersistedIncident?.Id == id
                ? PersistedIncident
                : null;

            return Task.FromResult(existing);
        }

        public Task<IncidentReference?> GetByJavaIncidentIdAsync(
            long javaIncidentId,
            CancellationToken cancellationToken = default
        )
        {
            var existing =
                PersistedIncident?.JavaIncidentId == javaIncidentId
                    ? PersistedIncident
                    : null;

            return Task.FromResult(existing);
        }

        public Task<IReadOnlyList<IncidentReference>> GetPagedAsync(
            RegulationStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default
        )
        {
            IReadOnlyList<IncidentReference> incidents =
                Array.Empty<IncidentReference>();

            return Task.FromResult(incidents);
        }

        public Task<int> CountAsync(
            RegulationStatus? status,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(0);
        }

        public Task<IncidentReference?> GetTrackedByJavaIncidentIdAsync(
            long javaIncidentId,
            CancellationToken cancellationToken = default
        )
        {
            return GetByJavaIncidentIdAsync(
                javaIncidentId,
                cancellationToken
            );
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default
        )
        {
            return Task.CompletedTask;
        }

        public Task AddAsync(
            IncidentReference incidentReference,
            CancellationToken cancellationToken = default
        )
        {
            InsertAttempts++;

            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;

                throw new TimeoutException(
                    "Simulated Oracle timeout."
                );
            }

            PersistedIncident = incidentReference;

            return Task.CompletedTask;
        }
    }
}
