namespace Stormaid.Application.DTOs.Integration;

/// <summary>
/// Representa uma mensagem de integração pendente
/// de publicação no RabbitMQ.
/// </summary>
public sealed record PendingOutboxMessage(
    long Id,
    string EventId,
    string EventType,
    string RoutingKey,
    string Payload,
    int Attempts
);