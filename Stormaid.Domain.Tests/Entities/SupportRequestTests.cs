using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class SupportRequestTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static SupportRequest CreateRequest()
    {
        return new SupportRequest(
            1,
            2,
            10,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            "Additional rescue support is required.",
            UtcNow
        );
    }

    [Test]
    public void Constructor_WithValidData_ShouldCreateOpenRequest()
    {
        var request = CreateRequest();

        Assert.Multiple(() =>
        {
            Assert.That(request.IncidentReferenceId, Is.EqualTo(1));
            Assert.That(request.IncidentDispatchId, Is.EqualTo(2));
            Assert.That(request.RequestedByResponderUserId, Is.EqualTo(10));

            Assert.That(
                request.RequestedInstitutionType,
                Is.EqualTo(ResponderInstitutionType.FIRE_DEPARTMENT)
            );

            Assert.That(request.Status,
                Is.EqualTo(SupportRequestStatus.OPEN));

            Assert.That(request.ReviewedByRegulatorUserId, Is.Null);
            Assert.That(request.ReviewJustification, Is.Null);
            Assert.That(request.ReviewedAt, Is.Null);
            Assert.That(request.CreatedAt, Is.EqualTo(UtcNow));
        });
    }

    [Test]
    public void Constructor_WithoutInstitution_ShouldCreateRequest()
    {
        var request = new SupportRequest(
            1, 2, 10, null,
            "Additional rescue support is required.",
            UtcNow
        );

        Assert.That(request.RequestedInstitutionType, Is.Null);
        Assert.That(request.Status, Is.EqualTo(SupportRequestStatus.OPEN));
    }

    [TestCase(0, 2, 10)]
    [TestCase(1, 0, 10)]
    [TestCase(1, 2, 0)]
    [TestCase(-1, 2, 10)]
    public void Constructor_WithInvalidId_ShouldThrow(
        long incidentId,
        long dispatchId,
        long responderId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SupportRequest(
                incidentId,
                dispatchId,
                responderId,
                null,
                "Additional rescue support is required.",
                UtcNow
            )
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Short")]
    public void Constructor_WithInvalidReason_ShouldThrow(string reason)
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(
                1, 2, 10, null,
                reason,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithLongReason_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportRequest(
                1, 2, 10, null,
                new string('A', 1001),
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidInstitution_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SupportRequest(
                1, 2, 10,
                (ResponderInstitutionType)999,
                "Additional rescue support is required.",
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
            new SupportRequest(
                1, 2, 10, null,
                "Additional rescue support is required.",
                localTime
            )
        );
    }

    [Test]
    public void Approve_WhenOpen_ShouldApproveRequest()
    {
        var request = CreateRequest();
        var reviewedAt = UtcNow.AddMinutes(5);

        request.Approve(
            20,
            "Support approved after human assessment.",
            reviewedAt
        );

        Assert.Multiple(() =>
        {
            Assert.That(request.Status,
                Is.EqualTo(SupportRequestStatus.APPROVED));

            Assert.That(request.ReviewedByRegulatorUserId, Is.EqualTo(20));

            Assert.That(request.ReviewJustification,
                Is.EqualTo("Support approved after human assessment."));

            Assert.That(request.ReviewedAt, Is.EqualTo(reviewedAt));
        });
    }

    [Test]
    public void Decline_WhenOpen_ShouldDeclineRequest()
    {
        var request = CreateRequest();

        request.Decline(
            20,
            "Additional dispatch is not required.",
            UtcNow.AddMinutes(5)
        );

        Assert.That(request.Status,
            Is.EqualTo(SupportRequestStatus.DECLINED));

        Assert.That(request.ReviewedByRegulatorUserId, Is.EqualTo(20));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Approve_WithInvalidRegulatorId_ShouldThrow(long id)
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            request.Approve(
                id,
                "Support approved after assessment.",
                UtcNow.AddMinutes(1)
            )
        );

        Assert.That(request.Status, Is.EqualTo(SupportRequestStatus.OPEN));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Short")]
    public void Approve_WithInvalidJustification_ShouldThrow(
        string justification)
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() =>
            request.Approve(
                20,
                justification,
                UtcNow.AddMinutes(1)
            )
        );

        Assert.That(request.ReviewedAt, Is.Null);
    }

    [Test]
    public void Approve_WithLongJustification_ShouldThrow()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() =>
            request.Approve(
                20,
                new string('A', 1001),
                UtcNow.AddMinutes(1)
            )
        );
    }

    [Test]
    public void Approve_WithEarlierDate_ShouldThrow()
    {
        var request = CreateRequest();

        Assert.Throws<ArgumentException>(() =>
            request.Approve(
                20,
                "Support approved after assessment.",
                UtcNow.AddMinutes(-1)
            )
        );

        Assert.That(request.Status, Is.EqualTo(SupportRequestStatus.OPEN));
    }

    [Test]
    public void Approve_WithNonUtcDate_ShouldThrow()
    {
        var request = CreateRequest();

        var localTime = DateTime.SpecifyKind(
            UtcNow.AddMinutes(1),
            DateTimeKind.Local
        );

        Assert.Throws<ArgumentException>(() =>
            request.Approve(
                20,
                "Support approved after assessment.",
                localTime
            )
        );
    }

    [Test]
    public void Decline_WhenAlreadyApproved_ShouldThrow()
    {
        var request = CreateRequest();

        request.Approve(
            20,
            "Support approved after assessment.",
            UtcNow.AddMinutes(1)
        );

        Assert.Throws<InvalidOperationException>(() =>
            request.Decline(
                21,
                "Support declined after new assessment.",
                UtcNow.AddMinutes(2)
            )
        );

        Assert.That(request.Status,
            Is.EqualTo(SupportRequestStatus.APPROVED));
    }

    [Test]
    public void Approve_ShouldTrimJustification()
    {
        var request = CreateRequest();

        request.Approve(
            20,
            "  Support approved after assessment.  ",
            UtcNow.AddMinutes(1)
        );

        Assert.That(
            request.ReviewJustification,
            Is.EqualTo("Support approved after assessment.")
        );
    }
}
