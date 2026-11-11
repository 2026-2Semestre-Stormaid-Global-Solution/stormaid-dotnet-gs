
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class IncidentDispatchTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static IncidentDispatch CreateDispatch()
    {
        return new IncidentDispatch(
            1,
            2,
            3,
            4,
            "Simulated rescue operation.",
            UtcNow
        );
    }

    [Test]
    public void Constructor_WithValidData_ShouldCreatePendingDispatch()
    {
        var dispatch = CreateDispatch();

        Assert.Multiple(() =>
        {
            Assert.That(dispatch.IncidentReferenceId, Is.EqualTo(1));
            Assert.That(dispatch.RegulationDecisionId, Is.EqualTo(2));
            Assert.That(dispatch.RescueUnitId, Is.EqualTo(3));
            Assert.That(dispatch.CreatedByRegulatorUserId, Is.EqualTo(4));

            Assert.That(
                dispatch.Status,
                Is.EqualTo(DispatchStatus.PENDING_ACCEPTANCE)
            );

            Assert.That(dispatch.AcceptedByResponderUserId, Is.Null);
            Assert.That(dispatch.DispatchedAt, Is.EqualTo(UtcNow));
            Assert.That(dispatch.Version, Is.EqualTo(0));
        });
    }

    [TestCase(0, 2, 3, 4)]
    [TestCase(1, 0, 3, 4)]
    [TestCase(1, 2, 0, 4)]
    [TestCase(1, 2, 3, 0)]
    public void Constructor_WithInvalidId_ShouldThrow(
        long incidentId,
        long decisionId,
        long unitId,
        long regulatorId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentDispatch(
                incidentId,
                decisionId,
                unitId,
                regulatorId,
                null,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithLongNotes_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentDispatch(
                1, 2, 3, 4,
                new string('A', 1001),
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithNonUtcDate_ShouldThrow()
    {
        var localTime = DateTime.SpecifyKind(
            UtcNow,
            DateTimeKind.Local
        );

        Assert.Throws<ArgumentException>(() =>
            new IncidentDispatch(
                1, 2, 3, 4,
                null,
                localTime
            )
        );
    }

    [Test]
    public void Accept_WhenPending_ShouldUpdateStatus()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.ACCEPTED));
            Assert.That(dispatch.AcceptedByResponderUserId, Is.EqualTo(10));
            Assert.That(dispatch.AcceptedAt, Is.EqualTo(UtcNow.AddMinutes(1)));
            Assert.That(dispatch.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void Accept_WithInvalidResponderId_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            dispatch.Accept(0, UtcNow.AddMinutes(1))
        );

        Assert.That(dispatch.Version, Is.EqualTo(0));
    }

    [Test]
    public void Accept_RepeatedBySameResponder_ShouldNotIncrementVersion()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));
        dispatch.Accept(10, UtcNow.AddMinutes(2));

        Assert.That(dispatch.Version, Is.EqualTo(1));
        Assert.That(dispatch.AcceptedAt, Is.EqualTo(UtcNow.AddMinutes(1)));
    }

    [Test]
    public void Accept_ByDifferentResponder_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.Accept(11, UtcNow.AddMinutes(2))
        );

        Assert.That(dispatch.AcceptedByResponderUserId, Is.EqualTo(10));
        Assert.That(dispatch.Version, Is.EqualTo(1));
    }

    [Test]
    public void StartRoute_WithoutAcceptance_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.StartRoute(UtcNow.AddMinutes(1))
        );

        Assert.That(dispatch.Status,
            Is.EqualTo(DispatchStatus.PENDING_ACCEPTANCE));
    }

    [Test]
    public void CompleteFlow_ShouldReachResolved()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));
        dispatch.StartRoute(UtcNow.AddMinutes(2));
        dispatch.Arrive(UtcNow.AddMinutes(3));
        dispatch.Resolve(UtcNow.AddMinutes(4));

        Assert.Multiple(() =>
        {
            Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.RESOLVED));
            Assert.That(dispatch.ResolvedAt, Is.EqualTo(UtcNow.AddMinutes(4)));
            Assert.That(dispatch.Version, Is.EqualTo(4));
        });
    }

    [Test]
    public void Arrive_BeforeStartingRoute_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.Arrive(UtcNow.AddMinutes(2))
        );

        Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.ACCEPTED));
    }

    [Test]
    public void Resolve_BeforeArrival_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));
        dispatch.StartRoute(UtcNow.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.Resolve(UtcNow.AddMinutes(3))
        );

        Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.EN_ROUTE));
    }

    [Test]
    public void Cancel_WhenPending_ShouldCancelDispatch()
    {
        var dispatch = CreateDispatch();

        dispatch.Cancel(UtcNow.AddMinutes(1));

        Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.CANCELLED));
        Assert.That(dispatch.CancelledAt, Is.EqualTo(UtcNow.AddMinutes(1)));
        Assert.That(dispatch.Version, Is.EqualTo(1));
    }

    [Test]
    public void Cancel_WhenEnRoute_ShouldCancelDispatch()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));
        dispatch.StartRoute(UtcNow.AddMinutes(2));
        dispatch.Cancel(UtcNow.AddMinutes(3));

        Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.CANCELLED));
        Assert.That(dispatch.Version, Is.EqualTo(3));
    }

    [Test]
    public void Cancel_WhenResolved_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        dispatch.Accept(10, UtcNow.AddMinutes(1));
        dispatch.StartRoute(UtcNow.AddMinutes(2));
        dispatch.Arrive(UtcNow.AddMinutes(3));
        dispatch.Resolve(UtcNow.AddMinutes(4));

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.Cancel(UtcNow.AddMinutes(5))
        );

        Assert.That(dispatch.Status, Is.EqualTo(DispatchStatus.RESOLVED));
    }

    [Test]
    public void Accept_WhenCancelled_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        dispatch.Cancel(UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            dispatch.Accept(10, UtcNow.AddMinutes(2))
        );
    }

    [Test]
    public void Accept_WithEarlierTimestamp_ShouldThrow()
    {
        var dispatch = CreateDispatch();

        Assert.Throws<ArgumentException>(() =>
            dispatch.Accept(10, UtcNow.AddMinutes(-1))
        );

        Assert.That(dispatch.Version, Is.EqualTo(0));
        Assert.That(dispatch.AcceptedAt, Is.Null);
    }
}
