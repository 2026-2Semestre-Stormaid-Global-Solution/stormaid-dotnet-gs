namespace Stormaid.Application.Interfaces;

/// <summary>
/// Define o contrato para publicar eventos de integração
/// previamente registrados na Outbox.
/// </summary>
public interface IOutboxEventPublisher
{
    /// <summary>
    /// Publica um evento e aguarda a confirmação
    /// de recebimento pelo broker.
    /// </summary>
    /// <param name="eventId">
    /// Identificador único utilizado na deduplicação.
    /// </param>
    /// <param name="eventType">
    /// Nome e versão do evento de integração.
    /// </param>
    /// <param name="routingKey">
    /// Chave de roteamento da mensagem.
    /// </param>
    /// <param name="payload">
    /// Conteúdo JSON persistido na Outbox.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da publicação.
    /// </param>
    Task PublishAsync(
        string eventId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default
    );
}