
using Stormaid.Application.DTOs.Integration;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

/// <summary>
/// Testes do processamento de ocorrências recebidas do Java.
/// </summary>
public sealed class IncidentCreatedEventServiceTests
{
    // 1 - Processamento de uma ocorrência válida
    [Fact]
    public async Task ProcessAsync_ShouldCreateIncident_WhenEventIsValid()
    {
        var repository = new FakeIncidentReferenceRepository();
        var service = new IncidentCreatedEventService(repository);

        var result = await service.ProcessAsync(CreateValidEvent());

        Assert.Equal(150, result.JavaIncidentId);
        Assert.Equal(25, result.RequesterUserId);
        Assert.Equal(
            RegulationStatus.PENDING_REVIEW,
            result.RegulationStatus
        );
        Assert.Equal(1, repository.InsertCount);
    }

    // 2 - Evento duplicado não deve gerar nova inserção
    [Fact]
    public async Task ProcessAsync_ShouldNotDuplicateExistingIncident()
    {
        var repository = new FakeIncidentReferenceRepository();
        var service = new IncidentCreatedEventService(repository);
        var message = CreateValidEvent();

        var first = await service.ProcessAsync(message);
        var second = await service.ProcessAsync(message);

        Assert.Same(first, second);
        Assert.Equal(1, repository.InsertCount);
    }

    // 3 - Identificador inválido deve ser rejeitado
    [Fact]
    public async Task ProcessAsync_ShouldRejectInvalidIncidentId()
    {
        var repository = new FakeIncidentReferenceRepository();
        var service = new IncidentCreatedEventService(repository);

        var message = CreateValidEvent() with
        {
            IncidentId = 0
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(message)
        );

        Assert.Equal(0, repository.InsertCount);
    }

    // 4 - Localização inválida deve ser rejeitada
    [Fact]
    public async Task ProcessAsync_ShouldRejectInvalidLatitude()
    {
        var repository = new FakeIncidentReferenceRepository();
        var service = new IncidentCreatedEventService(repository);

        var message = CreateValidEvent() with
        {
            Location = CreateLocation() with
            {
                Latitude = 91m
            }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ProcessAsync(message)
        );

        Assert.Equal(0, repository.InsertCount);
    }

    // 5 - Dados válidos utilizados nos testes
    private static IncidentCreatedEvent CreateValidEvent()
    {
        return new IncidentCreatedEvent(
            IncidentId: 150,
            RequesterUserId: 25,
            RecipientType: RecipientType.SELF,
            ChannelType: ChannelType.TEXT_INPUT,
            EmergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            PublicStatus: "CREATED",
            Location: CreateLocation(),
            CreatedAt: DateTimeOffset.UtcNow
        );
    }

    private static IncidentEventLocation CreateLocation()
    {
        return new IncidentEventLocation(
            Source: "GPS",
            Latitude: -23.5505m,
            Longitude: -46.6333m,
            AddressText: "São Paulo - SP",
            ReferencePoint: null,
            AccuracyMeters: null,
            LocationCapturedAt: null
        );
    }

    [Fact]
    public async Task ProcessAsync_ShouldPropagateError_WhenDatabaseIsUnavailable()
    {
        var repository = new FakeIncidentReferenceRepository
        {
            SimulateDatabaseFailure = true
        };

        var service = new IncidentCreatedEventService(repository);

        await Assert.ThrowsAsync<TimeoutException>(
            () => service.ProcessAsync(CreateValidEvent())
        );

        Assert.Equal(0, repository.InsertCount);
    }

    // 6 - Repositório simulado para isolar o Oracle
    private sealed class FakeIncidentReferenceRepository
        : IIncidentReferenceRepository
    {
        private readonly Dictionary<long, IncidentReference> _incidents =
            new();

        public int InsertCount { get; private set; }

        public bool SimulateDatabaseFailure { get; set; }

        public Task<IncidentReference?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var incident = id > 0
                ? _incidents.Values.FirstOrDefault(x => x.Id == id)
                : null;

            return Task.FromResult(incident);
        }
        
        
        public Task<IncidentReference?> GetTrackedByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            return GetByIdAsync(id, cancellationToken);
        }

        public Task<IncidentReference?> GetByJavaIncidentIdAsync(
            long javaIncidentId,
            CancellationToken cancellationToken = default)
        {
            _incidents.TryGetValue(javaIncidentId, out var incident);

            return Task.FromResult(incident);
        }

        public Task<IReadOnlyList<IncidentReference>> GetPagedAsync(
            RegulationStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IncidentReference> incidents =
                Array.Empty<IncidentReference>();

            return Task.FromResult(incidents);
        }

        public Task<int> CountAsync(
            RegulationStatus? status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<IncidentReference?> GetTrackedByJavaIncidentIdAsync(
            long javaIncidentId,
            CancellationToken cancellationToken = default)
        {
            return GetByJavaIncidentIdAsync(
                javaIncidentId,
                cancellationToken
            );
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddAsync(
            IncidentReference incidentReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(incidentReference);

            if (SimulateDatabaseFailure)
            {
                throw new TimeoutException(
                    "Simulated Oracle database timeout."
                );
            }

            if (!_incidents.TryAdd(
                    incidentReference.JavaIncidentId,
                    incidentReference))
            {
                throw new InvalidOperationException(
                    "Incident already exists."
                );
            }

            InsertCount++;

            return Task.CompletedTask;
        }
    }
}
