
using NUnit.Framework;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Tests.Entities;

/// <summary>
/// Testes das regras de início da análise
/// de ocorrências na central de regulação.
/// </summary>
[TestFixture]
public sealed class IncidentReferenceReviewTests
{
    private static readonly DateTime InitialDate = new(
        2026, 11, 14, 18, 0, 0,
        DateTimeKind.Utc
    );

    /// <summary>
    /// Verifica a transição de PENDING_REVIEW
    /// para UNDER_REVIEW.
    /// </summary>
    [Test]
    public void BeginReview_ShouldChangeStatusToUnderReview()
    {
        var incident = CreateIncident();

        incident.BeginReview(InitialDate.AddMinutes(1));

        Assert.That(
            incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.UNDER_REVIEW)
        );

        Assert.That(incident.Version, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifica que repetir o início da análise
    /// não altera novamente o estado ou a versão.
    /// </summary>
    [Test]
    public void BeginReview_ShouldBeIdempotent()
    {
        var incident = CreateIncident();

        incident.BeginReview(InitialDate.AddMinutes(1));

        var version = incident.Version;
        var updatedAt = incident.UpdatedAt;

        incident.BeginReview(InitialDate.AddMinutes(2));

        Assert.That(
            incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.UNDER_REVIEW)
        );

        Assert.That(incident.Version, Is.EqualTo(version));
        Assert.That(incident.UpdatedAt, Is.EqualTo(updatedAt));
    }

    /// <summary>
    /// Verifica que não é permitido reiniciar
    /// a análise após avançar para despacho.
    /// </summary>
    [Test]
    public void BeginReview_ShouldRejectInvalidTransition()
    {
        var incident = CreateIncident();

        incident.BeginReview(InitialDate.AddMinutes(1));
        incident.AwaitDispatch(InitialDate.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(
            () => incident.BeginReview(
                InitialDate.AddMinutes(3)
            )
        );

        Assert.That(
            incident.RegulationStatus,
            Is.EqualTo(RegulationStatus.AWAITING_DISPATCH)
        );
    }

    /// <summary>
    /// Verifica o incremento da versão pública
    /// utilizada na integração com a API Java.
    /// </summary>
    [Test]
    public void AdvancePublicStatusVersion_ShouldIncrementVersion()
    {
        var incident = CreateIncident();

        incident.AdvancePublicStatusVersion(InitialDate.AddMinutes(1));

        Assert.That(incident.PublicStatusVersion, Is.EqualTo(2));
        Assert.That(incident.Version, Is.EqualTo(1));

        incident.AdvancePublicStatusVersion(InitialDate.AddMinutes(2));

        Assert.That(incident.PublicStatusVersion, Is.EqualTo(3));
        Assert.That(incident.Version, Is.EqualTo(2));
    }

    /// <summary>
    /// Cria uma ocorrência válida para os testes.
    /// </summary>
    private static IncidentReference CreateIncident()
    {
        return new IncidentReference(
            javaIncidentId: 8,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "São Paulo - SP",
            javaCreatedAt: null,
            utcNow: InitialDate
        );
    }
}
