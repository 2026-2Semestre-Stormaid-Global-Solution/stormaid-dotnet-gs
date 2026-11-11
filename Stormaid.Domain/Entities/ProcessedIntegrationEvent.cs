using Stormaid.Domain.Common;

namespace Stormaid.Domain.Entities;

public class ProcessedIntegrationEvent : Entity
{
    public string EventId { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public DateTime ProcessedAt { get; private set; }

    private ProcessedIntegrationEvent() {}

    public ProcessedIntegrationEvent(
        string eventId,
        string eventType,
        DateTime processedAt)
    {
        var validatedEventId = ValidateText(
            eventId,
            120,
            nameof(eventId)
        );

        var validatedEventType = ValidateText(
            eventType,
            80,
            nameof(eventType)
        );

        ValidateUtc(processedAt);

        EventId = validatedEventId;
        EventType = validatedEventType;
        ProcessedAt = processedAt;
    }

    private static string ValidateText(
        string value,
        int maxLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value is required.",
                parameterName
            );
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value must contain at most {maxLength} characters.",
                parameterName
            );
        }

        return normalized;
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
}