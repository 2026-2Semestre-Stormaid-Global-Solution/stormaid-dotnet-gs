using Stormaid.Application.Interfaces;
using Stormaid.Infrastructure.Data;
using Stormaid.Infrastructure.Data.Entities;

namespace Stormaid.Infrastructure.Messaging;

/// <summary>
/// Implementa o registro de mensagens de integração
/// utilizando o Entity Framework Core e o Oracle.
/// </summary>
public sealed class IntegrationEventOutbox : IIntegrationEventOutbox
{
    private readonly StormaidDbContext _context;

    /// <summary>
    /// Inicializa o repositório de eventos de integração.
    /// </summary>
    /// <param name="context">
    /// Contexto de persistência Oracle do Storm Aid.
    /// </param>
    public IntegrationEventOutbox(StormaidDbContext context)
    {
        _context = context ??
                   throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Adiciona uma mensagem ao contexto atual, sem executar
    /// SaveChangesAsync, permitindo a gravação atômica com
    /// as alterações de negócio.
    /// </summary>
    public async Task EnqueueAsync(
        string eventId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage(
            eventId: eventId,
            eventType: eventType,
            routingKey: routingKey,
            payload: payload,
            utcNow: DateTime.UtcNow
        );

        await _context.OutboxMessages.AddAsync(
            message,
            cancellationToken
        );
    }
}