using Moq;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

public sealed class IncidentReferenceQueryServiceTests
{
    [Fact]
    public async Task GetByJavaIncidentIdAsync_ShouldReturnIncident_WhenExists()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        var incident = new IncidentReference(
            javaIncidentId: 8,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: DateTime.UtcNow
        );

        repository
            .Setup(x => x.GetByJavaIncidentIdAsync(
                8,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        var result = await service.GetByJavaIncidentIdAsync(8);

        Assert.NotNull(result);
        Assert.Equal(8L, result.JavaIncidentId);
        Assert.Equal(
            RegulationStatus.PENDING_REVIEW,
            result.RegulationStatus
        );
    }

    [Fact]
    public async Task GetByJavaIncidentIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        repository
            .Setup(x => x.GetByJavaIncidentIdAsync(
                999,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IncidentReference?)null);

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        var result = await service.GetByJavaIncidentIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByJavaIncidentIdAsync_ShouldRejectInvalidId()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetByJavaIncidentIdAsync(0)
        );

        repository.Verify(
            x => x.GetByJavaIncidentIdAsync(
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
