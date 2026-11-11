using NUnit.Framework;
using Stormaid.Domain.Entities;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class ProcessedIntegrationEventTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidData_ShouldCreateEvent()
    {
        var integrationEvent = new ProcessedIntegrationEvent(
            "evt-stormaid-301-1",
            "incident.created.v1",
            UtcNow
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                integrationEvent.EventId,
                Is.EqualTo("evt-stormaid-301-1")
            );

            Assert.That(
                integrationEvent.EventType,
                Is.EqualTo("incident.created.v1")
            );

            Assert.That(integrationEvent.ProcessedAt, Is.EqualTo(UtcNow));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidEventId_ShouldThrow(string eventId)
    {
        Assert.Throws<ArgumentException>(() =>
            new ProcessedIntegrationEvent(
                eventId,
                "incident.created.v1",
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithLongEventId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProcessedIntegrationEvent(
                new string('A', 121),
                "incident.created.v1",
                UtcNow
            )
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidEventType_ShouldThrow(
        string eventType)
    {
        Assert.Throws<ArgumentException>(() =>
            new ProcessedIntegrationEvent(
                "evt-stormaid-301-1",
                eventType,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithLongEventType_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProcessedIntegrationEvent(
                "evt-stormaid-301-1",
                new string('A', 81),
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithNonUtcDate_ShouldThrow()
    {
        var localDate = DateTime.SpecifyKind(
            UtcNow,
            DateTimeKind.Local
        );

        Assert.Throws<ArgumentException>(() =>
            new ProcessedIntegrationEvent(
                "evt-stormaid-301-1",
                "incident.created.v1",
                localDate
            )
        );
    }

    [Test]
    public void Constructor_WithWhitespace_ShouldTrimFields()
    {
        var integrationEvent = new ProcessedIntegrationEvent(
            "  evt-stormaid-301-1  ",
            "  incident.created.v1  ",
            UtcNow
        );

        Assert.That(
            integrationEvent.EventId,
            Is.EqualTo("evt-stormaid-301-1")
        );

        Assert.That(
            integrationEvent.EventType,
            Is.EqualTo("incident.created.v1")
        );
    }

    [Test]
    public void Constructor_WithMaximumLengths_ShouldSucceed()
    {
        var integrationEvent = new ProcessedIntegrationEvent(
            new string('A', 120),
            new string('B', 80),
            UtcNow
        );

        Assert.That(integrationEvent.EventId.Length, Is.EqualTo(120));
        Assert.That(integrationEvent.EventType.Length, Is.EqualTo(80));
    }
}
