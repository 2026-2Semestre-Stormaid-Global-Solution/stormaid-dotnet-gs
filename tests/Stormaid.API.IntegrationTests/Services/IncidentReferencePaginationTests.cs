using Moq;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

/// <summary>
/// Testes da paginação e dos filtros da fila de regulação.
/// </summary>
public sealed class IncidentReferencePaginationTests
{
    /// <summary>
    /// Verifica os registros e metadados de uma página.
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldReturnIncidentsAndPagination()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        IReadOnlyList<IncidentReference> incidents =
        [
            CreateIncident(8),
            CreateIncident(7),
            CreateIncident(6)
        ];

        repository
            .Setup(x => x.CountAsync(
                RegulationStatus.PENDING_REVIEW,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        repository
            .Setup(x => x.GetPagedAsync(
                RegulationStatus.PENDING_REVIEW,
                0,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incidents);

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        var result = await service.GetPagedAsync(
            RegulationStatus.PENDING_REVIEW,
            page: 0,
            pageSize: 3
        );

        Assert.Equal(3, result.Content.Count);
        Assert.Equal(0, result.Page.Number);
        Assert.Equal(3, result.Page.Size);
        Assert.Equal(7, result.Page.TotalElements);
        Assert.Equal(3, result.Page.TotalPages);

        Assert.Equal(8L, result.Content[0].JavaIncidentId);
        Assert.Equal(7L, result.Content[1].JavaIncidentId);
        Assert.Equal(6L, result.Content[2].JavaIncidentId);

        repository.Verify(
            x => x.GetPagedAsync(
                RegulationStatus.PENDING_REVIEW,
                0,
                3,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Verifica a consulta sem filtro e com valores padrão.
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldUseDefaults_WhenParametersAreOmitted()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        IReadOnlyList<IncidentReference> incidents = [];

        repository
            .Setup(x => x.CountAsync(
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        repository
            .Setup(x => x.GetPagedAsync(
                null,
                0,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incidents);

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        var result = await service.GetPagedAsync(null);

        Assert.Empty(result.Content);
        Assert.Equal(0, result.Page.Number);
        Assert.Equal(10, result.Page.Size);
        Assert.Equal(0, result.Page.TotalElements);
        Assert.Equal(0, result.Page.TotalPages);
    }

    /// <summary>
    /// Verifica o retorno de uma página além do total disponível.
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldReturnEmptyPage_WhenBeyondLastPage()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        IReadOnlyList<IncidentReference> incidents = [];

        repository
            .Setup(x => x.CountAsync(
                RegulationStatus.PENDING_REVIEW,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        repository
            .Setup(x => x.GetPagedAsync(
                RegulationStatus.PENDING_REVIEW,
                3,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(incidents);

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        var result = await service.GetPagedAsync(
            RegulationStatus.PENDING_REVIEW,
            page: 3,
            pageSize: 3
        );

        Assert.Empty(result.Content);
        Assert.Equal(3, result.Page.Number);
        Assert.Equal(7, result.Page.TotalElements);
        Assert.Equal(3, result.Page.TotalPages);
    }

    /// <summary>
    /// Verifica a rejeição de parâmetros de paginação inválidos.
    /// </summary>
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task GetPagedAsync_ShouldRejectInvalidPagination(
        int page,
        int pageSize)
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetPagedAsync(
                null,
                page,
                pageSize)
        );

        repository.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifica a rejeição de um status inexistente.
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldRejectInvalidStatus()
    {
        var repository = new Mock<IIncidentReferenceRepository>();

        var service = new IncidentReferenceQueryService(
            repository.Object
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetPagedAsync(
                (RegulationStatus)999,
                page: 0,
                pageSize: 10)
        );

        repository.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cria uma referência válida para os testes de paginação.
    /// </summary>
    private static IncidentReference CreateIncident(
        long javaIncidentId)
    {
        return new IncidentReference(
            javaIncidentId: javaIncidentId,
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
    }
}
