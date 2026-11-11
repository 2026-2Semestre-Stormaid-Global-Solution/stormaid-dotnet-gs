
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class RescueUnitTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static RescueUnit CreateUnit()
    {
        return new RescueUnit(
            "RESCUE-001",
            "Fire Rescue Unit",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            5,
            true,
            UtcNow
        );
    }

    [Test]
    public void Constructor_WithValidData_ShouldCreateUnit()
    {
        var unit = CreateUnit();

        Assert.Multiple(() =>
        {
            Assert.That(unit.UnitCode, Is.EqualTo("RESCUE-001"));
            Assert.That(unit.DisplayName, Is.EqualTo("Fire Rescue Unit"));
            Assert.That(unit.Capacity, Is.EqualTo(5));
            Assert.That(unit.Active, Is.True);
            Assert.That(unit.Version, Is.EqualTo(0));
            Assert.That(unit.CreatedAt, Is.EqualTo(UtcNow));
            Assert.That(
                unit.OperationalStatus,
                Is.EqualTo(UnitOperationalStatus.AVAILABLE)
            );
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(51)]
    public void Constructor_WithInvalidCapacity_ShouldThrow(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RescueUnit(
                "RESCUE-001",
                "Fire Rescue Unit",
                ResponderInstitutionType.FIRE_DEPARTMENT,
                capacity,
                true,
                UtcNow
            )
        );
    }

    [TestCase("")]
    [TestCase("AB")]
    [TestCase("   ")]
    public void Constructor_WithInvalidUnitCode_ShouldThrow(string code)
    {
        Assert.Throws<ArgumentException>(() =>
            new RescueUnit(
                code,
                "Fire Rescue Unit",
                ResponderInstitutionType.FIRE_DEPARTMENT,
                5,
                true,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidInstitution_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RescueUnit(
                "RESCUE-001",
                "Fire Rescue Unit",
                (ResponderInstitutionType)999,
                5,
                true,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithNonUtcDate_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new RescueUnit(
                "RESCUE-001",
                "Fire Rescue Unit",
                ResponderInstitutionType.FIRE_DEPARTMENT,
                5,
                true,
                DateTime.SpecifyKind(UtcNow, DateTimeKind.Local)
            )
        );
    }

    [Test]
    public void MarkAsBusy_WhenAvailable_ShouldUpdateStatus()
    {
        var unit = CreateUnit();
        var updatedAt = UtcNow.AddMinutes(1);

        unit.MarkAsBusy(updatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(
                unit.OperationalStatus,
                Is.EqualTo(UnitOperationalStatus.BUSY)
            );
            Assert.That(unit.Version, Is.EqualTo(1));
            Assert.That(unit.UpdatedAt, Is.EqualTo(updatedAt));
        });
    }

    [Test]
    public void MarkAsBusy_WhenAlreadyBusy_ShouldNotIncrementVersion()
    {
        var unit = CreateUnit();

        unit.MarkAsBusy(UtcNow.AddMinutes(1));
        unit.MarkAsBusy(UtcNow.AddMinutes(2));

        Assert.That(unit.Version, Is.EqualTo(1));
    }

    [Test]
    public void MarkAsBusy_WhenOutOfService_ShouldThrow()
    {
        var unit = CreateUnit();

        unit.MarkOutOfService(UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            unit.MarkAsBusy(UtcNow.AddMinutes(2))
        );
    }

    [Test]
    public void Deactivate_ShouldDisableUnit()
    {
        var unit = CreateUnit();

        unit.Deactivate(UtcNow.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(unit.Active, Is.False);
            Assert.That(
                unit.OperationalStatus,
                Is.EqualTo(UnitOperationalStatus.OUT_OF_SERVICE)
            );
            Assert.That(unit.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void MarkAsBusy_WhenInactive_ShouldThrow()
    {
        var unit = CreateUnit();

        unit.Deactivate(UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            unit.MarkAsBusy(UtcNow.AddMinutes(2))
        );
    }

    [Test]
    public void Activate_ShouldNotAutomaticallyMakeUnitAvailable()
    {
        var unit = CreateUnit();

        unit.Deactivate(UtcNow.AddMinutes(1));
        unit.Activate(UtcNow.AddMinutes(2));

        Assert.That(unit.Active, Is.True);
        Assert.That(
            unit.OperationalStatus,
            Is.EqualTo(UnitOperationalStatus.OUT_OF_SERVICE)
        );
    }

    [Test]
    public void UpdateDetails_WithInvalidCapacity_ShouldPreserveData()
    {
        var unit = CreateUnit();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            unit.UpdateDetails(
                "RESCUE-002",
                "Updated Rescue Unit",
                ResponderInstitutionType.CIVIL_DEFENSE,
                60,
                false,
                UtcNow.AddMinutes(1)
            )
        );

        Assert.Multiple(() =>
        {
            Assert.That(unit.UnitCode, Is.EqualTo("RESCUE-001"));
            Assert.That(unit.Capacity, Is.EqualTo(5));
            Assert.That(unit.Version, Is.EqualTo(0));
        });
    }

    [Test]
    public void UpdateDetails_WithSameData_ShouldNotIncrementVersion()
    {
        var unit = CreateUnit();

        unit.UpdateDetails(
            "RESCUE-001",
            "Fire Rescue Unit",
            ResponderInstitutionType.FIRE_DEPARTMENT,
            5,
            true,
            UtcNow.AddMinutes(1)
        );

        Assert.That(unit.Version, Is.EqualTo(0));
        Assert.That(unit.UpdatedAt, Is.EqualTo(UtcNow));
    }
}
