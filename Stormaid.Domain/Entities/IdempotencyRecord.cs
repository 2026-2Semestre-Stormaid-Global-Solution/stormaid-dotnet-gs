
using System.Text.Json;
using Stormaid.Domain.Common;
using Stormaid.Domain.Enums;

namespace Stormaid.Domain.Entities;

public class IdempotencyRecord : Entity
{
    public long ActorUserId { get; private set; }

    public string Operation { get; private set; } = null!;

    public string IdempotencyKey { get; private set; } = null!;

    public string RequestHash { get; private set; } = null!;

    public int? ResponseStatusCode { get; private set; }

    public string? ResponseBodyJson { get; private set; }

    public IdempotencyState State { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    private IdempotencyRecord() {}

    public IdempotencyRecord(
        long actorUserId,
        string operation,
        string idempotencyKey,
        string requestHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        ValidateId(actorUserId, nameof(actorUserId));

        var normalizedOperation = NormalizeText(
            operation,
            80,
            nameof(operation)
        );

        var normalizedKey = NormalizeText(
            idempotencyKey,
            100,
            nameof(idempotencyKey)
        );

        var normalizedHash = NormalizeHash(requestHash);

        ValidateUtc(createdAt);
        ValidateUtc(expiresAt);

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException(
                "Expiration must be later than creation time.",
                nameof(expiresAt)
            );
        }

        ActorUserId = actorUserId;
        Operation = normalizedOperation;
        IdempotencyKey = normalizedKey;
        RequestHash = normalizedHash;

        State = IdempotencyState.PROCESSING;

        ResponseStatusCode = null;
        ResponseBodyJson = null;

        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public void Complete(
        int responseStatusCode,
        string? responseBodyJson)
    {
        EnsureProcessing();

        if (responseStatusCode is < 200 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseStatusCode),
                "HTTP status code must be between 200 and 599."
            );
        }

        if (responseBodyJson is not null)
        {
            using var document =
                JsonDocument.Parse(responseBodyJson);
        }

        ResponseStatusCode = responseStatusCode;
        ResponseBodyJson = responseBodyJson;
        State = IdempotencyState.COMPLETED;
    }

    public void MarkFailedRetryable()
    {
        if (State == IdempotencyState.FAILED_RETRYABLE)
            return;

        EnsureProcessing();

        State = IdempotencyState.FAILED_RETRYABLE;
    }

    public void RestartProcessing()
    {
        if (State != IdempotencyState.FAILED_RETRYABLE)
        {
            throw new InvalidOperationException(
                "Only retryable failures can be restarted."
            );
        }

        ResponseStatusCode = null;
        ResponseBodyJson = null;

        State = IdempotencyState.PROCESSING;
    }

    public void EnsureSameRequest(
        long actorUserId,
        string operation,
        string idempotencyKey,
        string requestHash)
    {
        ValidateId(actorUserId, nameof(actorUserId));

        var normalizedOperation = NormalizeText(
            operation,
            80,
            nameof(operation)
        );

        var normalizedKey = NormalizeText(
            idempotencyKey,
            100,
            nameof(idempotencyKey)
        );

        var normalizedHash = NormalizeHash(requestHash);

        if (ActorUserId != actorUserId ||
            Operation != normalizedOperation ||
            IdempotencyKey != normalizedKey ||
            RequestHash != normalizedHash)
        {
            throw new InvalidOperationException("Idempotency key conflicts with an existing request.");
        }
    }

    public bool IsExpired(DateTime utcNow)
    {
        ValidateUtc(utcNow);

        return utcNow >= ExpiresAt;
    }

    private void EnsureProcessing()
    {
        if (State != IdempotencyState.PROCESSING)
        {
            throw new InvalidOperationException("The idempotency record is not processing.");
        }
    }

    private static void ValidateId(
        long id,
        string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Identifier must be greater than zero."
            );
        }
    }

    private static string NormalizeText(
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

    private static string NormalizeHash(string requestHash)
    {
        if (string.IsNullOrWhiteSpace(requestHash))
        {
            throw new ArgumentException(
                "Request hash is required.",
                nameof(requestHash)
            );
        }

        var normalized = requestHash.Trim().ToLowerInvariant();

        if (normalized.Length != 64)
        {
            throw new ArgumentException(
                "Request hash must contain 64 hexadecimal characters.",
                nameof(requestHash)
            );
        }

        foreach (var character in normalized)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                throw new ArgumentException(
                    "Request hash must be hexadecimal.",
                    nameof(requestHash)
                );
            }
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
