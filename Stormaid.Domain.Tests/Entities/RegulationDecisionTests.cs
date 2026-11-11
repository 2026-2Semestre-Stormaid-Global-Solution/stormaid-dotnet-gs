
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

[TestFixture]
public class RegulationDecisionTests
{
    private static readonly DateTime UtcNow =
        new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static RegulationDecision CreateApprovedDecision()
    {
        return new RegulationDecision(
            1,
            20,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            "Dispatch approved after human assessment.",
            UtcNow
        );
    }

    [Test]
    public void Constructor_WithApprovedDispatch_ShouldCreateDecision()
    {
        var decision = CreateApprovedDecision();

        Assert.Multiple(() =>
        {
            Assert.That(decision.IncidentReferenceId, Is.EqualTo(1));
            Assert.That(decision.RegulatorUserId, Is.EqualTo(20));

            Assert.That(
                decision.DecisionType,
                Is.EqualTo(RegulationDecisionType.DISPATCH_APPROVED)
            );

            Assert.That(
                decision.InstitutionType,
                Is.EqualTo(ResponderInstitutionType.FIRE_DEPARTMENT)
            );

            Assert.That(
                decision.Justification,
                Is.EqualTo("Dispatch approved after human assessment.")
            );

            Assert.That(decision.DecidedAt, Is.EqualTo(UtcNow));
        });
    }

    [Test]
    public void Constructor_WithNoDispatchRequired_ShouldCreateDecision()
    {
        var decision = new RegulationDecision(
            1,
            20,
            RegulationDecisionType.NO_DISPATCH_REQUIRED,
            null,
            "Human assessment determined no dispatch is required.",
            UtcNow
        );

        Assert.That(
            decision.DecisionType,
            Is.EqualTo(RegulationDecisionType.NO_DISPATCH_REQUIRED)
        );

        Assert.That(decision.InstitutionType, Is.Null);
    }

    [Test]
    public void Constructor_WithMoreInformationRequired_ShouldCreateDecision()
    {
        var decision = new RegulationDecision(
            1,
            20,
            RegulationDecisionType.MORE_INFORMATION_REQUIRED,
            null,
            "Additional information is required for assessment.",
            UtcNow
        );

        Assert.That(
            decision.DecisionType,
            Is.EqualTo(RegulationDecisionType.MORE_INFORMATION_REQUIRED)
        );

        Assert.That(decision.InstitutionType, Is.Null);
    }

    [Test]
    public void Constructor_WithApprovedDispatchWithoutInstitution_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new RegulationDecision(
                1,
                20,
                RegulationDecisionType.DISPATCH_APPROVED,
                null,
                "Dispatch approved after assessment.",
                UtcNow
            )
        );
    }

    [TestCase(RegulationDecisionType.MORE_INFORMATION_REQUIRED)]
    [TestCase(RegulationDecisionType.NO_DISPATCH_REQUIRED)]
    public void Constructor_WithInstitutionWithoutApproval_ShouldThrow(
        RegulationDecisionType decisionType)
    {
        Assert.Throws<ArgumentException>(() =>
            new RegulationDecision(
                1,
                20,
                decisionType,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                "Human assessment completed successfully.",
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidDecisionType_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegulationDecision(
                1,
                20,
                (RegulationDecisionType)999,
                null,
                "Human assessment completed successfully.",
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithInvalidInstitutionType_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegulationDecision(
                1,
                20,
                RegulationDecisionType.DISPATCH_APPROVED,
                (ResponderInstitutionType)999,
                "Dispatch approved after assessment.",
                UtcNow
            )
        );
    }

    [TestCase(0, 20)]
    [TestCase(1, 0)]
    public void Constructor_WithInvalidId_ShouldThrow(
        long incidentReferenceId,
        long regulatorUserId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegulationDecision(
                incidentReferenceId,
                regulatorUserId,
                RegulationDecisionType.DISPATCH_APPROVED,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                "Dispatch approved after assessment.",
                UtcNow
            )
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Too short")]
    public void Constructor_WithInvalidJustification_ShouldThrow(
        string justification)
    {
        Assert.Throws<ArgumentException>(() =>
            new RegulationDecision(
                1,
                20,
                RegulationDecisionType.DISPATCH_APPROVED,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                justification,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithWhitespace_ShouldTrimJustification()
    {
        var decision = new RegulationDecision(
            1,
            20,
            RegulationDecisionType.DISPATCH_APPROVED,
            ResponderInstitutionType.FIRE_DEPARTMENT,
            "  Dispatch approved after assessment.  ",
            UtcNow
        );

        Assert.That(
            decision.Justification,
            Is.EqualTo("Dispatch approved after assessment.")
        );
    }

    [Test]
    public void Constructor_WithLongJustification_ShouldThrow()
    {
        var justification = new string('A', 1001);

        Assert.Throws<ArgumentException>(() =>
            new RegulationDecision(
                1,
                20,
                RegulationDecisionType.DISPATCH_APPROVED,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                justification,
                UtcNow
            )
        );
    }

    [Test]
    public void Constructor_WithNonUtcDate_ShouldThrow()
    {
        var localTime =
            DateTime.SpecifyKind(UtcNow, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            new RegulationDecision(
                1,
                20,
                RegulationDecisionType.DISPATCH_APPROVED,
                ResponderInstitutionType.FIRE_DEPARTMENT,
                "Dispatch approved after assessment.",
                localTime
            )
        );
    }
}
