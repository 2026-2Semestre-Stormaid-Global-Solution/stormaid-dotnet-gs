namespace Stormaid.Infrastructure.Data.Entities;

/// <summary>
/// Representa uma mensagem de integração pendente
/// de publicação no broker RabbitMQ.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; private set; }

    public string EventId { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public string RoutingKey { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    /// <summary>
    /// Cria uma mensagem pendente de publicação.
    /// </summary>
    public OutboxMessage(
        string eventId,
        string eventType,
        string routingKey,
        string payload,
        DateTime utcNow)
    {
        EventId = ValidateText(eventId, nameof(eventId), 120);
        EventType = ValidateText(eventType, nameof(eventType), 100);
        RoutingKey = ValidateText(routingKey, nameof(routingKey), 120);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "Payload cannot be empty.",
                nameof(payload)
            );
        }

        ValidateUtc(utcNow);

        Payload = payload;
        CreatedAt = utcNow;
        PublishedAt = null;
        Attempts = 0;
        LastError = null;
    }

    /// <summary>
    /// Registra que o broker confirmou
    /// a publicação da mensagem.
    /// </summary>
    public void MarkPublished(DateTime utcNow)
    {
        if (PublishedAt.HasValue)
        {
            return;
        }

        ValidateUtc(utcNow);

        PublishedAt = utcNow;
        LastError = null;
    }

    /// <summary>
    /// Registra uma falha de publicação
    /// para permitir uma nova tentativa.
    /// </summary>
    public void RegisterFailure(string error)
    {
        Attempts = checked(Attempts + 1);

        LastError = string.IsNullOrWhiteSpace(error)
            ? "Unknown publishing error."
            : error.Trim()[..Math.Min(error.Trim().Length, 1000)];
    }

    private static string ValidateText(
        string? value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be empty.",
                parameterName
            );
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength} characters.",
                parameterName
            );
        }

        return normalized;
    }

    private static void ValidateUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Date must be in UTC.",
                nameof(utcNow)
            );
        }
    }
}
