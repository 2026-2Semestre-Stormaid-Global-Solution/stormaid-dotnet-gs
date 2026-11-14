using Stormaid.Application.DTOs.Integration;

namespace Stormaid.Application.Interfaces;

/// <summary>
/// Define as operações de consulta e atualização
/// das mensagens armazenadas na Outbox.
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Consulta um lote de mensagens ainda não publicadas.
    /// </summary>
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingBatchAsync(
        int maxCount,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Marca uma mensagem como publicada
    /// após confirmação do RabbitMQ.
    /// </summary>
    Task MarkPublishedAsync(
        long messageId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Registra uma falha de publicação
    /// e incrementa o contador de tentativas.
    /// </summary>
    Task RegisterFailureAsync(
        long messageId,
        string error,
        CancellationToken cancellationToken = default
    );
}