namespace Stormaid.Infrastructure.Messaging;

/// <summary>
/// Representa as configurações de conexão
/// com o servidor RabbitMQ.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Habilita ou desabilita o consumidor de eventos.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Endereço do servidor RabbitMQ.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Porta utilizada pela conexão AMQP.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Virtual host utilizado pelo RabbitMQ.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Usuário de autenticação.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Senha de autenticação.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Nome da fila de ocorrências criadas pelo Java.
    /// </summary>
    public string QueueName { get; set; } = "incident.created.queue";

    /// <summary>
    /// Indica se a conexão deve utilizar TLS.
    /// </summary>
    public bool UseTls { get; set; } = false;
}