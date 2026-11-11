
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class DispatchStatusHistoryTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static DispatchStatusHistory CreateHistory(
        DispatchStatus? previousStatus,
        DispatchStatus newStatus,
        DispatchActorRole role,
        string? note = null,
        DateTime? utcNow = null)
    {
        return new DispatchStatusHistory(
            301,
            previousStatus,
            newStatus,
            10,
            role,
            note,
            utcNow ?? UtcNow
        );
    }

    [Test]
    public void Constructor_WithInitialStatus_ShouldCreateHistory()
    {
        var history = CreateHistory(
            null,
            DispatchStatus.PENDING_ACCEPTANCE,
            DispatchActorRole.REGULATOR
        );

        Assert.Multiple(() =>
        {
            Assert.That(history.IncidentDispatchId, Is.EqualTo(301));
            Assert.That(history.PreviousStatus, Is.Null);
            Assert.That(
                history.NewStatus,
                Is.EqualTo(DispatchStatus.PENDING_ACCEPTANCE)
            );
            Assert.That(history.ChangedByUserId, Is.EqualTo(10));
            Assert.That(
                history.ChangedByRole,
                Is.EqualTo(DispatchActorRole.REGULATOR)
            );
            Assert.That(history.ChangedAt, Is.EqualTo(UtcNow));
        });
    }

    [Test]
    public void Constructor_WithAcceptance_ShouldCreateHistory()
    {
        var history = CreateHistory(
            DispatchStatus.PENDING_ACCEPTANCE,
            DispatchStatus.ACCEPTED,
            DispatchActorRole.RESPONDER
        );

        Assert.That(
            history.PreviousStatus,
            Is.EqualTo(DispatchStatus.PENDING_ACCEPTANCE)
        );

        Assert.That(
            history.NewStatus,
            Is.EqualTo(DispatchStatus.ACCEPTED)
        );
    }

    [TestCase(DispatchStatus.PENDING_ACCEPTANCE)]
    [TestCase(DispatchStatus.ACCEPTED)]
    [TestCase(DispatchStatus.EN_ROUTE)]
    [TestCase(DispatchStatus.ON_SITE)]
    public void Constructor_WithAuthorizedCancellation_ShouldCreateHistory(
        DispatchStatus previousStatus)
    {
        var history = CreateHistory(
            previousStatus,
            DispatchStatus.CANCELLED,
            DispatchActorRole.REGULATOR
        );

        Assert.That(
            history.NewStatus,
            Is.EqualTo(DispatchStatus.CANCELLED)
        );
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithInvalidDispatchId_ShouldThrow(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatchStatusHistory(
                id,
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                10,
                DispatchActorRole.REGULATOR,
                null,
                UtcNow
            )
        );
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithInvalidActorId_ShouldThrow(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatchStatusHistory(
                301,
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                id,
                DispatchActorRole.REGULATOR,
                null,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidInitialStatus_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateHistory(
                null,
                DispatchStatus.ACCEPTED,
                DispatchActorRole.REGULATOR
            )
        );
    }

    [Test]
    public void Constructor_WithUnauthorizedAcceptance_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateHistory(
                DispatchStatus.PENDING_ACCEPTANCE,
                DispatchStatus.ACCEPTED,
                DispatchActorRole.REGULATOR
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidBackwardTransition_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateHistory(
                DispatchStatus.ON_SITE,
                DispatchStatus.EN_ROUTE,
                DispatchActorRole.RESPONDER
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidPreviousStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateHistory(
                (DispatchStatus)999,
                DispatchStatus.ACCEPTED,
                DispatchActorRole.RESPONDER
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidNewStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateHistory(
                DispatchStatus.PENDING_ACCEPTANCE,
                (DispatchStatus)999,
                DispatchActorRole.RESPONDER
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidActorRole_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateHistory(
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                (DispatchActorRole)999
            )
        );
    }

    [Test]
    public void Constructor_WithWhitespaceNote_ShouldTrimNote()
    {
        var history = CreateHistory(
            null,
            DispatchStatus.PENDING_ACCEPTANCE,
            DispatchActorRole.REGULATOR,
            "  Dispatch created successfully.  "
        );

        Assert.That(
            history.Note,
            Is.EqualTo("Dispatch created successfully.")
        );
    }

    [Test]
    public void Constructor_WithLongNote_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateHistory(
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                DispatchActorRole.REGULATOR,
                new string('A', 501)
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
            CreateHistory(
                null,
                DispatchStatus.PENDING_ACCEPTANCE,
                DispatchActorRole.REGULATOR,
                utcNow: localTime
            )
        );
    }
}
