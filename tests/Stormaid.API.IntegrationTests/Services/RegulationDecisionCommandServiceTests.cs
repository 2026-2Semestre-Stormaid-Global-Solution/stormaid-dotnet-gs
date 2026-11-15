
using Moq;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

public sealed class RegulationDecisionCommandServiceTests
{
    private const long JavaIncidentId = 900004;
    private const long RegulatorUserId = 10;
    private const long IncidentReferenceId = 50;

    private const string Justification = "Ocorrência avaliada pela equipe de regulação.";

    [Fact]
    public async Task RegisterAsync_ShouldApproveDispatch()
    {
        var incident = CreateIncident();
        var mocks = CreateMocks(incident);

        var result = await mocks.Service.RegisterAsync(
            JavaIncidentId,
            RegulatorUserId,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            Justification);

        Assert.True(result.Changed);
        Assert.Equal(RegulationStatus.AWAITING_DISPATCH, result.RegulationStatus);
        Assert.Equal(2L, result.PublicStatusVersion);
        Assert.Equal(RegulatorUserId, result.RegulatorUserId);

        mocks.Decisions.Verify(
            repository => repository.AddAsync(
                It.Is<RegulationDecision>(decision =>
                    decision.DecisionType == RegulationDecisionType.DISPATCH_APPROVED &&
                    decision.InstitutionType == ResponderInstitutionType.FIRE_DEPARTMENT),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.Outbox.Verify(
            outbox => outbox.EnqueueAsync(
                $"evt-regulation-{JavaIncidentId}-2",
                "incident.status.changed.v1",
                "incident.status.changed.v1",
                It.Is<string>(payload =>
                    payload.Contains("\"status\":\"AWAITING_DISPATCH\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.Incidents.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCloseIncident_WhenDispatchIsNotRequired()
    {
        var incident = CreateIncident();
        var mocks = CreateMocks(incident);

        var result = await mocks.Service.RegisterAsync(
            JavaIncidentId,
            RegulatorUserId,
            RegulationDecisionType.NO_DISPATCH_REQUIRED,
            null,
            Justification);

        Assert.True(result.Changed);
        Assert.Equal(RegulationStatus.CLOSED, result.RegulationStatus);
        Assert.Equal(2L, result.PublicStatusVersion);

        mocks.Decisions.Verify(
            repository => repository.AddAsync(
                It.Is<RegulationDecision>(decision =>
                    decision.DecisionType == RegulationDecisionType.NO_DISPATCH_REQUIRED),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.Outbox.Verify(
            outbox => outbox.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(payload =>
                    payload.Contains("\"status\":\"CLOSED\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldKeepIncidentUnderReview_WhenMoreInformationIsRequired()
    {
        var incident = CreateIncident();
        var originalVersion = incident.Version;
        var mocks = CreateMocks(incident);

        var result = await mocks.Service.RegisterAsync(
            JavaIncidentId,
            RegulatorUserId,
            RegulationDecisionType.MORE_INFORMATION_REQUIRED,
            null,
            Justification);

        Assert.True(result.Changed);
        Assert.Equal(RegulationStatus.UNDER_REVIEW, result.RegulationStatus);
        Assert.Equal(1L, result.PublicStatusVersion);
        Assert.Equal(originalVersion, incident.Version);

        mocks.Decisions.Verify(
            repository => repository.AddAsync(
                It.IsAny<RegulationDecision>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.Outbox.Verify(
            outbox => outbox.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mocks.Incidents.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnExistingDecision_WhenRequestIsRepeated()
    {
        var incident = CreateIncident();
        var utcNow = DateTime.UtcNow;

        incident.AwaitDispatch(utcNow);
        incident.AdvancePublicStatusVersion(utcNow);

        var existingDecision = new RegulationDecision(
            IncidentReferenceId,
            RegulatorUserId,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            Justification,
            utcNow);

        var mocks = CreateMocks(incident, existingDecision);

        var result = await mocks.Service.RegisterAsync(
            JavaIncidentId,
            RegulatorUserId,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            Justification);

        Assert.False(result.Changed);
        Assert.Equal(RegulationStatus.AWAITING_DISPATCH, result.RegulationStatus);
        Assert.Equal(RegulatorUserId, result.RegulatorUserId);

        mocks.Decisions.Verify(
            repository => repository.AddAsync(
                It.IsAny<RegulationDecision>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        mocks.Incidents.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        mocks.Outbox.Verify(
            outbox => outbox.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldRejectInvalidIncidentStatus()
    {
        var incident = CreateIncident();

        incident.AwaitDispatch(DateTime.UtcNow);

        var mocks = CreateMocks(incident);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mocks.Service.RegisterAsync(
                JavaIncidentId,
                RegulatorUserId,
                RegulationDecisionType.NO_DISPATCH_REQUIRED,
                null,
                Justification));

        mocks.Decisions.Verify(
            repository => repository.AddAsync(
                It.IsAny<RegulationDecision>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldRejectMissingIncident()
    {
        var mocks = CreateMocks(null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => mocks.Service.RegisterAsync(
                JavaIncidentId,
                RegulatorUserId,
                RegulationDecisionType.DISPATCH_APPROVED,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                Justification));

        mocks.Incidents.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldRejectApprovalWithoutInstitution()
    {
        var mocks = CreateMocks(CreateIncident());

        await Assert.ThrowsAsync<ArgumentException>(
            () => mocks.Service.RegisterAsync(
                JavaIncidentId,
                RegulatorUserId,
                RegulationDecisionType.DISPATCH_APPROVED,
                null,
                Justification));

        mocks.Incidents.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IncidentReference CreateIncident()
    {
        var utcNow = DateTime.UtcNow;

        var incident = new IncidentReference(
            javaIncidentId: JavaIncidentId,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "Ocorrência de teste",
            javaCreatedAt: null,
            utcNow: utcNow.AddMinutes(-10));

        incident.BeginReview(RegulatorUserId, utcNow.AddMinutes(-5));
        incident.AdvancePublicStatusVersion(utcNow.AddMinutes(-5));

        typeof(IncidentReference)
            .GetProperty(nameof(IncidentReference.Id))!
            .SetValue(incident, IncidentReferenceId);

        return incident;
    }

    private static (
        RegulationDecisionCommandService Service,
        Mock<IIncidentReferenceRepository> Incidents,
        Mock<IRegulationDecisionRepository> Decisions,
        Mock<IIntegrationEventOutbox> Outbox
    ) CreateMocks(
        IncidentReference? incident,
        RegulationDecision? latestDecision = null)
    {
        var incidents = new Mock<IIncidentReferenceRepository>();
        var decisions = new Mock<IRegulationDecisionRepository>();
        var outbox = new Mock<IIntegrationEventOutbox>();

        incidents.Setup(repository =>
                repository.GetTrackedByJavaIncidentIdAsync(
                    JavaIncidentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(incident);

        incidents.Setup(repository =>
                repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        decisions.Setup(repository =>
                repository.GetLatestByIncidentReferenceIdAsync(
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestDecision);

        decisions.Setup(repository =>
                repository.AddAsync(
                    It.IsAny<RegulationDecision>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        outbox.Setup(publisher =>
                publisher.EnqueueAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new RegulationDecisionCommandService(
            incidents.Object,
            decisions.Object,
            outbox.Object);

        return (service, incidents, decisions, outbox);
    }
}
