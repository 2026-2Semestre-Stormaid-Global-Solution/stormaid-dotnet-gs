
using System.Text.Json;
using System.Text.Json.Serialization;
using Stormaid.Application.DTOs.Integration;
using Stormaid.Domain.Enums;

namespace Stormaid.API.IntegrationTests.Services;

public sealed class IncidentCreatedEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(
                    namingPolicy: null,
                    allowIntegerValues: false
                )
            }
        };

    private const string ValidPayload = """
        {
          "incidentId": 8,
          "requesterUserId": 1,
          "recipientType": "SELF",
          "channelType": "TEXT_INPUT",
          "emergencyCategory": "CLIMATE_DISASTER",
          "publicStatus": "RECEIVED",
          "location": {
            "source": "CURRENT_LOCATION",
            "latitude": -23.5505,
            "longitude": -46.6333,
            "addressText": "São Paulo - SP",
            "referencePoint": "Teste de concorrência"
          },
          "createdAt": "2026-11-14T19:38:41.068463Z"
        }
        """;

    [Fact]
    public void ShouldDeserializeJavaIncidentCreatedEvent()
    {
        var message = JsonSerializer.Deserialize<IncidentCreatedEvent>(
            ValidPayload,
            JsonOptions
        );

        Assert.NotNull(message);

        Assert.Equal(8L, message.IncidentId);
        Assert.Equal(1L, message.RequesterUserId);

        Assert.Equal(RecipientType.SELF, message.RecipientType);
        Assert.Equal(ChannelType.TEXT_INPUT, message.ChannelType);
        Assert.Equal(
            EmergencyCategory.CLIMATE_DISASTER,
            message.EmergencyCategory
        );

        Assert.Equal("RECEIVED", message.PublicStatus);

        Assert.NotNull(message.Location);

        Assert.Equal(-23.5505m, message.Location.Latitude);
        Assert.Equal(-46.6333m, message.Location.Longitude);
        Assert.Equal(
            "São Paulo - SP",
            message.Location.AddressText
        );

        Assert.Equal(
            DateTimeOffset.Parse("2026-11-14T19:38:41.068463Z"),
            message.CreatedAt
        );
    }

    [Fact]
    public void ShouldRejectUnknownJavaEnum()
    {
        var invalidPayload = ValidPayload.Replace(
            "\"SELF\"",
            "\"INVALID_RECIPIENT\""
        );

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IncidentCreatedEvent>(
                invalidPayload,
                JsonOptions
            )
        );
    }
}
