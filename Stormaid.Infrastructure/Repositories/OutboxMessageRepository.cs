
using Microsoft.EntityFrameworkCore;
using Stormaid.Application.DTOs.Integration;
using Stormaid.Application.Interfaces;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure.Repositories;

/// <summary>
/// Implementa a consulta e a atualização de mensagens
/// da Outbox utilizando Entity Framework Core e Oracle.
/// </summary>
public sealed class OutboxMessageRepository
    : IOutboxMessageRepository
{
    private const int MaxPublishingAttempts = 10;

    private readonly StormaidDbContext _context;

    /// <summary>
    /// Inicializa o repositório de mensagens da Outbox.
    /// </summary>
    public OutboxMessageRepository(StormaidDbContext context)
    {
        _context = context
            ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Recupera mensagens pendentes em ordem de criação,
    /// respeitando o limite de tentativas configurado.
    /// </summary>
    public async Task<IReadOnlyList<PendingOutboxMessage>>
        GetPendingBatchAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
    {
        if (maxCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount)
            );
        }

        return await _context.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.PublishedAt == null &&
                message.Attempts < MaxPublishingAttempts
            )
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Take(maxCount)
            .Select(message => new PendingOutboxMessage(
                message.Id,
                message.EventId,
                message.EventType,
                message.RoutingKey,
                message.Payload,
                message.Attempts
            ))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Confirma no Oracle que a mensagem foi publicada.
    /// </summary>
    public async Task MarkPublishedAsync(
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(
                item => item.Id == messageId,
                cancellationToken
            );

        if (message is null)
        {
            throw new KeyNotFoundException(
                $"Outbox message {messageId} was not found."
            );
        }

        if (message.PublishedAt.HasValue)
        {
            return;
        }

        message.MarkPublished(DateTime.UtcNow);

        await _context.SaveChangesAsync(
            cancellationToken
        );
    }

    /// <summary>
    /// Registra uma falha de publicação
    /// para permitir tentativas posteriores.
    /// </summary>
    public async Task RegisterFailureAsync(
        long messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(
                item => item.Id == messageId,
                cancellationToken
            );

        if (message is null)
        {
            throw new KeyNotFoundException(
                $"Outbox message {messageId} was not found."
            );
        }

        if (message.PublishedAt.HasValue)
        {
            return;
        }

        message.RegisterFailure(error);

        await _context.SaveChangesAsync(
            cancellationToken
        );
    }
}
