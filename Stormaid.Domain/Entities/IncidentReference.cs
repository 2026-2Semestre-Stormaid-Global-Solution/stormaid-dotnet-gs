
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class IncidentReference : Entity
{
    public long JavaIncidentId { get; private set; }
    public long RequesterUserId { get; private set; }

    public RecipientType RecipientType { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public EmergencyCategory EmergencyCategory { get; private set; }

    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? AddressText { get; private set; }

    public RegulationStatus RegulationStatus { get; private set; }

    public long? ReviewedByRegulatorUserId { get; private set; }

    public DateTime? ReviewStartedAt { get; private set; }

    public long PublicStatusVersion { get; private set; }

    public DateTime ReceivedAt { get; private set; }
    public DateTime? JavaCreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public long Version { get; private set; }

    private IncidentReference()
    {
    }

    public IncidentReference(
        long javaIncidentId,
        long requesterUserId,
        RecipientType recipientType,
        ChannelType channelType,
        EmergencyCategory emergencyCategory,
        decimal? latitude,
        decimal? longitude,
        string? addressText,
        DateTime? javaCreatedAt,
        DateTime utcNow)
    {
        ValidateId(javaIncidentId, nameof(javaIncidentId));
        ValidateId(requesterUserId, nameof(requesterUserId));

        ValidateEnum(recipientType, nameof(recipientType));
        ValidateEnum(channelType, nameof(channelType));
        ValidateEnum(emergencyCategory, nameof(emergencyCategory));

        ValidateUtc(utcNow);

        if (javaCreatedAt.HasValue)
            ValidateUtc(javaCreatedAt.Value);

        var normalizedAddress = NormalizeAddress(addressText);

        ValidateLocation(latitude, longitude, normalizedAddress);

        JavaIncidentId = javaIncidentId;
        RequesterUserId = requesterUserId;

        RecipientType = recipientType;
        ChannelType = channelType;
        EmergencyCategory = emergencyCategory;

        Latitude = latitude;
        Longitude = longitude;
        AddressText = normalizedAddress;

        RegulationStatus = RegulationStatus.PENDING_REVIEW;

        PublicStatusVersion = 0;
        Version = 0;

        ReceivedAt = utcNow;
        JavaCreatedAt = javaCreatedAt;
        UpdatedAt = utcNow;
    }

    public void BeginReview(DateTime utcNow)
    {
        TransitionTo(
            RegulationStatus.PENDING_REVIEW,
            RegulationStatus.UNDER_REVIEW,
            utcNow
        );
    }

    public void BeginReview(
        long regulatorUserId,
        DateTime utcNow)
    {
        ValidateId(
            regulatorUserId,
            nameof(regulatorUserId)
        );

        if (RegulationStatus == RegulationStatus.UNDER_REVIEW)
            return;

        TransitionTo(
            RegulationStatus.PENDING_REVIEW,
            RegulationStatus.UNDER_REVIEW,
            utcNow
        );

        ReviewedByRegulatorUserId = regulatorUserId;
        ReviewStartedAt = utcNow;
    }

    public void AwaitDispatch(DateTime utcNow)
    {
        TransitionTo(
            RegulationStatus.UNDER_REVIEW,
            RegulationStatus.AWAITING_DISPATCH,
            utcNow
        );
    }


    public void RequestMoreInformation(DateTime utcNow)
    {
        if (RegulationStatus != RegulationStatus.UNDER_REVIEW)
        {
            throw new InvalidOperationException(
                "Incident must be under review to request more information.");
        }

        ValidateUpdateTime(utcNow);
        Touch(utcNow);
    }


    public void MarkDispatched(DateTime utcNow)
    {
        TransitionTo(
            RegulationStatus.AWAITING_DISPATCH,
            RegulationStatus.DISPATCHED,
            utcNow
        );
    }

    public void Close(DateTime utcNow)
    {
        if (RegulationStatus == RegulationStatus.CLOSED)
            return;

        if (RegulationStatus is not (
            RegulationStatus.UNDER_REVIEW or
            RegulationStatus.AWAITING_DISPATCH or
            RegulationStatus.DISPATCHED))
        {
            throw new InvalidOperationException(
                "Incident cannot be closed in its current state."
            );
        }

        ValidateUpdateTime(utcNow);

        RegulationStatus = RegulationStatus.CLOSED;
        Touch(utcNow);
    }

    public void AdvancePublicStatusVersion(DateTime utcNow)
    {
        ValidateUpdateTime(utcNow);

        PublicStatusVersion = checked(PublicStatusVersion + 1);
        Touch(utcNow);
    }

    private void TransitionTo(
        RegulationStatus expected,
        RegulationStatus next,
        DateTime utcNow)
    {
        if (RegulationStatus == next)
            return;

        if (RegulationStatus != expected)
        {
            throw new InvalidOperationException(
                $"Cannot transition from {RegulationStatus} to {next}."
            );
        }

        ValidateUpdateTime(utcNow);

        RegulationStatus = next;
        Touch(utcNow);
    }

    private static void ValidateId(long value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Identifier must be greater than zero."
            );
        }
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Invalid enum value."
            );
        }
    }

    private static string? NormalizeAddress(string? addressText)
    {
        if (string.IsNullOrWhiteSpace(addressText))
            return null;

        var normalized = addressText.Trim();

        if (normalized.Length > 300)
        {
            throw new ArgumentException(
                "Address must contain at most 300 characters.",
                nameof(addressText)
            );
        }

        return normalized;
    }

    private static void ValidateLocation(
        decimal? latitude,
        decimal? longitude,
        string? addressText)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException(
                "Latitude and longitude must be provided together."
            );
        }

        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Latitude must be between -90 and 90."
            );
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                "Longitude must be between -180 and 180."
            );
        }

        if (!latitude.HasValue && addressText is null)
        {
            throw new ArgumentException(
                "Coordinates or an address must be provided."
            );
        }
    }

    private static void ValidateUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Date must be in UTC.",
                nameof(dateTime)
            );
        }
    }

    private void ValidateUpdateTime(DateTime utcNow)
    {
        ValidateUtc(utcNow);

        if (utcNow < UpdatedAt)
        {
            throw new ArgumentException(
                "Update time cannot be earlier than the previous update.",
                nameof(utcNow)
            );
        }
    }

    private void Touch(DateTime utcNow)
    {
        Version = checked(Version + 1);
        UpdatedAt = utcNow;
    }
}
