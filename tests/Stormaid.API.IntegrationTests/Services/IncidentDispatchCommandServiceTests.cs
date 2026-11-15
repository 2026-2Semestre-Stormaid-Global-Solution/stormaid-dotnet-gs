
using Moq;
using Stormaid.Application.DTOs.Request;
using Stormaid.Application.DTOs.Response;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

public sealed class IncidentDispatchCommandServiceTests
{
    private const long JavaIncidentId = 900010;
    private const long IncidentReferenceId = 50;
    private const long DecisionId = 60;
    private const long RescueUnitId = 70;
    private const long RegulatorUserId = 10;
    private const long ResponderUserId = 20;
    private const long DispatchId = 90;

    [Fact]
    public async Task CreateAsync_ShouldRejectMissingIncident()
    {
        var fixture = new Fixture(null, null, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.CreateAsync(
                CreateRequest(),
                RegulatorUserId));

        fixture.Dispatches.Verify(
            repository => repository.AddAsync(
                It.IsAny<IncidentDispatch>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectMissingDecision()
    {
        var fixture = new Fixture(
            CreateIncident(),
            null,
            CreateUnit());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.CreateAsync(
                CreateRequest(),
                RegulatorUserId));

        fixture.Dispatches.Verify(
            repository => repository.AddAsync(
                It.IsAny<IncidentDispatch>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectActiveDispatch()
    {
        var fixture = new Fixture(
            CreateIncident(),
            CreateDecision(),
            CreateUnit());

        var activeDispatch = new IncidentDispatch(
            IncidentReferenceId,
            DecisionId,
            RescueUnitId,
            RegulatorUserId,
            null,
            DateTime.UtcNow.AddMinutes(-1));

        fixture.Dispatches.Setup(repository =>
                repository.GetActiveByIncidentReferenceIdAsync(
                    IncidentReferenceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDispatch);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.CreateAsync(
                CreateRequest(),
                RegulatorUserId));

        fixture.Dispatches.Verify(
            repository => repository.AddAsync(
                It.IsAny<IncidentDispatch>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectUnavailableRescueUnit()
    {
        var unit = CreateUnit();
        unit.MarkAsBusy(DateTime.UtcNow.AddMinutes(-1));

        var fixture = new Fixture(
            CreateIncident(),
            CreateDecision(),
            unit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.CreateAsync(
                CreateRequest(),
                RegulatorUserId));

        fixture.Dispatches.Verify(
            repository => repository.AddAsync(
                It.IsAny<IncidentDispatch>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDispatchAndOutbox()
    {
        var incident = CreateIncident();
        var unit = CreateUnit();

        var fixture = new Fixture(
            incident,
            CreateDecision(),
            unit);

        var result = await fixture.Service.CreateAsync(
            CreateRequest(),
            RegulatorUserId);

        Assert.Equal(DispatchId, result.Id);
        Assert.Equal(IncidentReferenceId, result.IncidentReferenceId);
        Assert.Equal(DecisionId, result.RegulationDecisionId);
        Assert.Equal(RescueUnitId, result.RescueUnitId);
        Assert.Equal(DispatchStatus.PENDING_ACCEPTANCE, result.Status);

        Assert.Equal(RegulationStatus.DISPATCHED,
            incident.RegulationStatus);

        Assert.Equal(4L, incident.PublicStatusVersion);

        Assert.Equal(UnitOperationalStatus.BUSY,
            unit.OperationalStatus);

        Assert.NotNull(fixture.CreatedHistory);
        Assert.Equal(DispatchId,
            fixture.CreatedHistory.IncidentDispatchId);
        Assert.Null(fixture.CreatedHistory.PreviousStatus);
        Assert.Equal(DispatchStatus.PENDING_ACCEPTANCE,
            fixture.CreatedHistory.NewStatus);
        Assert.Equal(DispatchActorRole.REGULATOR,
            fixture.CreatedHistory.ChangedByRole);

        fixture.Dispatches.Verify(repository =>
            repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        fixture.Outbox.Verify(outbox =>
            outbox.EnqueueAsync(
                $"evt-dispatch-{JavaIncidentId}-4",
                "incident.status.changed.v1",
                "incident.status.changed.v1",
                It.Is<string>(payload =>
                    payload.Contains("\"status\":\"DISPATCHED\"")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        fixture.Transaction.Verify(transaction =>
            transaction.ExecuteAsync(
                It.IsAny<Func<CancellationToken,
                    Task<IncidentDispatchResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


[Fact]
public async Task CreateAsync_ShouldRejectUnapprovedDecision()
{
    var decision = new RegulationDecision(
        IncidentReferenceId,
        RegulatorUserId,
        RegulationDecisionType.NO_DISPATCH_REQUIRED,
        null,
        "Atendimento de resgate não necessário.",
        DateTime.UtcNow.AddMinutes(-5));

    SetId(decision, DecisionId);

    var fixture = new Fixture(
        CreateIncident(),
        decision,
        CreateUnit());

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => fixture.Service.CreateAsync(
            CreateRequest(),
            RegulatorUserId));

    fixture.Dispatches.Verify(
        repository => repository.AddAsync(
            It.IsAny<IncidentDispatch>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
}

[Fact]
public async Task CreateAsync_ShouldRejectMissingRescueUnit()
{
    var fixture = new Fixture(
        CreateIncident(),
        CreateDecision(),
        null);

    await Assert.ThrowsAsync<KeyNotFoundException>(
        () => fixture.Service.CreateAsync(
            CreateRequest(),
            RegulatorUserId));

    fixture.Dispatches.Verify(
        repository => repository.AddAsync(
            It.IsAny<IncidentDispatch>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
}

[Fact]
public async Task CreateAsync_ShouldRejectInvalidIncidentStatus()
{
    var incident = CreateIncident();

    incident.MarkDispatched(DateTime.UtcNow);

    var fixture = new Fixture(
        incident,
        CreateDecision(),
        CreateUnit());

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => fixture.Service.CreateAsync(
            CreateRequest(),
            RegulatorUserId));

    fixture.Dispatches.Verify(
        repository => repository.AddAsync(
            It.IsAny<IncidentDispatch>(),
            It.IsAny<CancellationToken>()),
        Times.Never);

    fixture.Outbox.Verify(
        outbox => outbox.EnqueueAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
}


    [Fact]
    public async Task AcceptAsync_ShouldAcceptAndRegisterHistory()
    {
        var dispatch = CreateDispatch();
        var fixture = new Fixture(null, null, null, dispatch);

        var result = await fixture.Service.AcceptAsync(DispatchId, ResponderUserId);

        Assert.Equal(DispatchStatus.ACCEPTED, result.Status);
        Assert.Equal(1L, result.Version);
        Assert.Equal(ResponderUserId, dispatch.AcceptedByResponderUserId);
        Assert.NotNull(dispatch.AcceptedAt);

        Assert.NotNull(fixture.CreatedHistory);
        Assert.Equal(DispatchStatus.PENDING_ACCEPTANCE, fixture.CreatedHistory.PreviousStatus);
        Assert.Equal(DispatchStatus.ACCEPTED, fixture.CreatedHistory.NewStatus);
        Assert.Equal(ResponderUserId, fixture.CreatedHistory.ChangedByUserId);
        Assert.Equal(DispatchActorRole.RESPONDER, fixture.CreatedHistory.ChangedByRole);

        fixture.Dispatches.Verify(repository => repository.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Outbox.Verify(outbox => outbox.EnqueueAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_ShouldBeIdempotentForSameResponder()
    {
        var fixture = new Fixture(null, null, null, CreateDispatch());

        await fixture.Service.AcceptAsync(DispatchId, ResponderUserId);
        var repeated = await fixture.Service.AcceptAsync(DispatchId, ResponderUserId);

        Assert.Equal(DispatchStatus.ACCEPTED, repeated.Status);
        Assert.Equal(1L, repeated.Version);
        fixture.Dispatches.Verify(repository => repository.AddHistoryAsync(
            It.IsAny<DispatchStatusHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Dispatches.Verify(repository => repository.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_ShouldRejectMissingDispatch()
    {
        var fixture = new Fixture(null, null, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Service.AcceptAsync(DispatchId, ResponderUserId));
    }

    [Fact]
    public async Task AcceptAsync_ShouldRejectResponderWithoutActiveAssignment()
    {
        var fixture = new Fixture(null, null, null, CreateDispatch());
        fixture.Dispatches.Setup(repository => repository.HasActiveAssignmentAsync(
                RescueUnitId, ResponderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Service.AcceptAsync(DispatchId, ResponderUserId));

        fixture.Dispatches.Verify(repository => repository.AddHistoryAsync(
            It.IsAny<DispatchStatusHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_ShouldRejectInvalidTransition()
    {
        var dispatch = CreateDispatch();
        dispatch.Cancel(DateTime.UtcNow);
        var fixture = new Fixture(null, null, null, dispatch);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptAsync(DispatchId, ResponderUserId));

        fixture.Dispatches.Verify(repository => repository.AddHistoryAsync(
            It.IsAny<DispatchStatusHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_ShouldRejectOtherResponderAfterAcceptance()
    {
        var fixture = new Fixture(null, null, null, CreateDispatch());
        await fixture.Service.AcceptAsync(DispatchId, ResponderUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AcceptAsync(DispatchId, ResponderUserId + 1));

        fixture.Dispatches.Verify(repository => repository.AddHistoryAsync(
            It.IsAny<DispatchStatusHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IncidentDispatch CreateDispatch()
    {
        var dispatch = new IncidentDispatch(
            IncidentReferenceId,
            DecisionId,
            RescueUnitId,
            RegulatorUserId,
            "Despacho de teste.",
            DateTime.UtcNow.AddMinutes(-5));

        SetId(dispatch, DispatchId);
        return dispatch;
    }

    private static CreateIncidentDispatchRequest CreateRequest()
    {
        return new CreateIncidentDispatchRequest(
            JavaIncidentId,
            RescueUnitId,
            "Equipe designada para atendimento.");
    }

    private static IncidentReference CreateIncident()
    {
        var now = DateTime.UtcNow;

        var incident = new IncidentReference(
            JavaIncidentId,
            1,
            RecipientType.SELF,
            ChannelType.TEXT_INPUT,
            EmergencyCategory.CLIMATE_DISASTER,
            -23.5505m,
            -46.6333m,
            "Ocorrência de teste",
            null,
            now.AddMinutes(-10));

        incident.BeginReview(
            RegulatorUserId,
            now.AddMinutes(-9));

        incident.AdvancePublicStatusVersion(
            now.AddMinutes(-9));

        incident.AwaitDispatch(now.AddMinutes(-8));
        incident.AdvancePublicStatusVersion(now.AddMinutes(-8));

        SetId(incident, IncidentReferenceId);

        return incident;
    }

    private static RegulationDecision CreateDecision()
    {
        var decision = new RegulationDecision(
            IncidentReferenceId,
            RegulatorUserId,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            "Despacho aprovado.",
            DateTime.UtcNow.AddMinutes(-7));

        SetId(decision, DecisionId);

        return decision;
    }

    private static RescueUnit CreateUnit()
    {
        var unit = new RescueUnit(
            "BOM-001",
            "Equipe de Bombeiros",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            4,
            true,
            DateTime.UtcNow.AddMinutes(-10));

        SetId(unit, RescueUnitId);

        return unit;
    }

    private static void SetId<T>(T entity, long id)
        where T : Stormaid.Domain.Common.Entity
    {
        typeof(T)
            .GetProperty(nameof(entity.Id))!
            .SetValue(entity, id);
    }

    private sealed class Fixture
    {
        public Mock<IIncidentDispatchRepository> Dispatches { get; } = new();
        public Mock<IIntegrationEventOutbox> Outbox { get; } = new();
        public Mock<ITransactionExecutor> Transaction { get; } = new();

        public DispatchStatusHistory? CreatedHistory { get; private set; }

        public IncidentDispatchCommandService Service { get; }

        private IncidentDispatch? _createdDispatch;

        public Fixture(
            IncidentReference? incident,
            RegulationDecision? decision,
            RescueUnit? unit,
            IncidentDispatch? existingDispatch = null)
        {
            var incidents = new Mock<IIncidentReferenceRepository>();
            var decisions = new Mock<IRegulationDecisionRepository>();
            var units = new Mock<IRescueUnitRepository>();

            incidents.Setup(repository =>
                    repository.GetTrackedByJavaIncidentIdAsync(
                        JavaIncidentId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(incident);

            decisions.Setup(repository =>
                    repository.GetLatestByIncidentReferenceIdAsync(
                        IncidentReferenceId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(decision);

            units.Setup(repository =>
                    repository.GetTrackedByIdAsync(
                        RescueUnitId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(unit);

            Dispatches.Setup(repository =>
                    repository.GetTrackedByIdAsync(
                        DispatchId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingDispatch);

            Dispatches.Setup(repository =>
                    repository.HasActiveAssignmentAsync(
                        RescueUnitId,
                        ResponderUserId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Dispatches.Setup(repository =>
                    repository.GetActiveByIncidentReferenceIdAsync(
                        IncidentReferenceId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync((IncidentDispatch?)null);

            Dispatches.Setup(repository =>
                    repository.AddAsync(
                        It.IsAny<IncidentDispatch>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IncidentDispatch, CancellationToken>(
                    (dispatch, _) => _createdDispatch = dispatch)
                .Returns(Task.CompletedTask);

            Dispatches.Setup(repository =>
                    repository.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ =>
                {
                    if (_createdDispatch is not null)
                    {
                        SetId(_createdDispatch, DispatchId);
                    }
                })
                .Returns(Task.CompletedTask);

            Dispatches.Setup(repository =>
                    repository.AddHistoryAsync(
                        It.IsAny<DispatchStatusHistory>(),
                        It.IsAny<CancellationToken>()))
                .Callback<DispatchStatusHistory, CancellationToken>(
                    (history, _) => CreatedHistory = history)
                .Returns(Task.CompletedTask);

            Outbox.Setup(outbox =>
                    outbox.EnqueueAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Transaction.Setup(transaction =>
                    transaction.ExecuteAsync(
                        It.IsAny<Func<CancellationToken,
                            Task<IncidentDispatchResponse>>>(),
                        It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken,
                        Task<IncidentDispatchResponse>> operation,
                    CancellationToken token) => operation(token));

            Service = new IncidentDispatchCommandService(
                incidents.Object,
                decisions.Object,
                units.Object,
                Dispatches.Object,
                Outbox.Object,
                Transaction.Object);
        }
    }
}

