namespace Stormaid.Domain.Enums;

public enum IdempotencyState
{
    PROCESSING,
    COMPLETED,
    FAILED_RETRYABLE
}
