namespace Stormaid.Application.Interfaces;

/// <summary>
/// Define o contrato para registrar eventos de integração
/// que serão publicados posteriormente no RabbitMQ.
/// </summary>
public interface IIntegrationEventOutbox
{
    /// <summary>
    /// Adiciona um evento pendente à unidade de trabalho atual.
    /// A operação não confirma a transação no banco.
    /// </summary>
    /// <param name="eventId">
    /// Identificador único do evento de integração.
    /// </param>
    /// <param name="eventType">
    /// Tipo e versão do evento.
    /// </param>
    /// <param name="routingKey">
    /// Chave de roteamento utilizada no RabbitMQ.
    /// </param>
    /// <param name="payload">
    /// Conteúdo JSON do evento.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento da operação.
    /// </param>
    /// <returns>
    /// Tarefa que representa o registro da mensagem pendente.
    /// </returns>
    Task EnqueueAsync(
        string eventId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default
    );
}