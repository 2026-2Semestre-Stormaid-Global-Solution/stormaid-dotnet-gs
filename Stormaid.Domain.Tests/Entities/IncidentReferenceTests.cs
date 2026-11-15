
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class IncidentReferenceTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static IncidentReference CreateIncident()
    {
        return new IncidentReference(
            301,
            101,
            RecipientType.THIRD_PARTY,
            ChannelType.TEXT_INPUT,
            EmergencyCategory.CLIMATE_DISASTER,
            null,
            null,
            "Rua Exemplo, 350 - São Paulo",
            UtcNow.AddMinutes(-1),
            UtcNow
        );
    }

    [Test]
    public void Constructor_WithValidData_ShouldCreatePendingIncident()
    {
        var incident = CreateIncident();

        Assert.Multiple(() =>
        {
            Assert.That(incident.JavaIncidentId, Is.EqualTo(301));
            Assert.That(incident.RequesterUserId, Is.EqualTo(101));
            Assert.That(incident.RegulationStatus,
                Is.EqualTo(RegulationStatus.PENDING_REVIEW));
            Assert.That(incident.PublicStatusVersion, Is.EqualTo(0));
            Assert.That(incident.Version, Is.EqualTo(0));
            Assert.That(incident.ReceivedAt, Is.EqualTo(UtcNow));
        });
    }

    [Test]
    public void Constructor_WithCoordinates_ShouldAcceptLocation()
    {
        var incident = new IncidentReference(
            301, 101,
            RecipientType.SELF,
            ChannelType.SOS_SILENT,
            EmergencyCategory.UNKNOWN_EMERGENCY,
            -23.5505m,
            -46.6333m,
            null,
            null,
            UtcNow
        );

        Assert.That(incident.Latitude, Is.EqualTo(-23.5505m));
        Assert.That(incident.Longitude, Is.EqualTo(-46.6333m));
    }

    [Test]
    public void Constructor_WithoutLocation_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentReference(
                301, 101,
                RecipientType.SELF,
                ChannelType.TEXT_INPUT,
                EmergencyCategory.FIRE_RESCUE,
                null, null, null, null, UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithOnlyLatitude_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentReference(
                301, 101,
                RecipientType.SELF,
                ChannelType.TEXT_INPUT,
                EmergencyCategory.FIRE_RESCUE,
                -23.5m, null, null, null, UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidLatitude_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentReference(
                301, 101,
                RecipientType.SELF,
                ChannelType.TEXT_INPUT,
                EmergencyCategory.FIRE_RESCUE,
                -91m, -46m, null, null, UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidCategory_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentReference(
                301, 101,
                RecipientType.SELF,
                ChannelType.TEXT_INPUT,
                (EmergencyCategory)999,
                null, null, "Rua Exemplo", null, UtcNow
            )
        );
    }

    [Test]
    public void BeginReview_ShouldChangeStatus()
    {
        var incident = CreateIncident();

        incident.BeginReview(UtcNow.AddMinutes(1));

        Assert.That(incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.UNDER_REVIEW));
        Assert.That(incident.Version, Is.EqualTo(1));
    }

    [Test]
    public void MarkDispatched_WithoutApprovalFlow_ShouldThrow()
    {
        var incident = CreateIncident();

        Assert.Throws<InvalidOperationException>(() =>
            incident.MarkDispatched(UtcNow.AddMinutes(1))
        );

        Assert.That(incident.Version, Is.EqualTo(0));
    }

    [Test]
    public void CompleteDispatchFlow_ShouldReachDispatched()
    {
        var incident = CreateIncident();

        incident.BeginReview(UtcNow.AddMinutes(1));
        incident.AwaitDispatch(UtcNow.AddMinutes(2));
        incident.MarkDispatched(UtcNow.AddMinutes(3));

        Assert.That(incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.DISPATCHED));
        Assert.That(incident.Version, Is.EqualTo(3));
    }

    [Test]
    public void Close_WhenUnderReview_ShouldCloseIncident()
    {
        var incident = CreateIncident();

        incident.BeginReview(UtcNow.AddMinutes(1));
        incident.Close(UtcNow.AddMinutes(2));

        Assert.That(incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.CLOSED));
    }

    [Test]
    public void AdvancePublicStatusVersion_ShouldIncrementVersion()
    {
        var incident = CreateIncident();

        incident.AdvancePublicStatusVersion(UtcNow.AddMinutes(1));

        Assert.That(incident.PublicStatusVersion, Is.EqualTo(2));
        Assert.That(incident.Version, Is.EqualTo(1));
    }

    [Test]
    public void BeginReview_WithEarlierTimestamp_ShouldThrow()
    {
        var incident = CreateIncident();

        Assert.Throws<ArgumentException>(() =>
            incident.BeginReview(UtcNow.AddMinutes(-1))
        );

        Assert.That(incident.Version, Is.EqualTo(0));
    }
}
