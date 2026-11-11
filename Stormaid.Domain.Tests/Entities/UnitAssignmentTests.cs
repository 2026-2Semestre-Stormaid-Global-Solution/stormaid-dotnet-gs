
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class UnitAssignmentTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidData_ShouldCreateActiveAssignment()
    {
        var assignment = new UnitAssignment(
            1,
            10,
            20,
            UtcNow
        );

        Assert.Multiple(() =>
        {
            Assert.That(assignment.RescueUnitId, Is.EqualTo(1));
            Assert.That(assignment.ResponderUserId, Is.EqualTo(10));
            Assert.That(assignment.AssignedByRegulatorUserId, Is.EqualTo(20));
            Assert.That(
                assignment.Status,
                Is.EqualTo(UnitAssignmentStatus.ACTIVE)
            );
            Assert.That(assignment.StartedAt, Is.EqualTo(UtcNow));
            Assert.That(assignment.CreatedAt, Is.EqualTo(UtcNow));
            Assert.That(assignment.EndedAt, Is.Null);
            Assert.That(assignment.Version, Is.EqualTo(0));
        });
    }

    [Test]
    public void End_ShouldCloseAssignment()
    {
        var assignment = new UnitAssignment(1, 10, 20, UtcNow);
        var endTime = UtcNow.AddHours(2);

        assignment.End(endTime);

        Assert.Multiple(() =>
        {
            Assert.That(
                assignment.Status,
                Is.EqualTo(UnitAssignmentStatus.ENDED)
            );
            Assert.That(assignment.EndedAt, Is.EqualTo(endTime));
            Assert.That(assignment.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void End_WhenAlreadyEnded_ShouldNotIncrementVersion()
    {
        var assignment = new UnitAssignment(1, 10, 20, UtcNow);

        assignment.End(UtcNow.AddHours(1));
        assignment.End(UtcNow.AddHours(2));

        Assert.That(assignment.Version, Is.EqualTo(1));
        Assert.That(
            assignment.EndedAt,
            Is.EqualTo(UtcNow.AddHours(1))
        );
    }

    [Test]
    public void End_WithEarlierDate_ShouldThrow()
    {
        var assignment = new UnitAssignment(1, 10, 20, UtcNow);

        Assert.Throws<ArgumentException>(() =>
            assignment.End(UtcNow.AddHours(-1))
        );

        Assert.That(
            assignment.Status,
            Is.EqualTo(UnitAssignmentStatus.ACTIVE)
        );
        Assert.That(assignment.Version, Is.EqualTo(0));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithInvalidRescueUnitId_ShouldThrow(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UnitAssignment(id, 10, 20, UtcNow)
        );
    }
}
