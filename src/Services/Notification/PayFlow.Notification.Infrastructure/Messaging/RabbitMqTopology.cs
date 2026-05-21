using RabbitMQ.Client;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Declares the retry topology idempotently. Layout:
/// <code>
///   exchange  payflow.notification.retry  (direct)
///       │  routing key = "schedule"
///       ▼
///   queue     payflow.notification.retry.wait
///             x-dead-letter-exchange = ""
///             x-dead-letter-routing-key = payflow.notification.retry.work
///             (messages publish with per-message TTL via BasicProperties.Expiration)
///       │  TTL expires → dead-lettered to default exchange
///       ▼
///   queue     payflow.notification.retry.work    ← consumer reads here
///
///   queue     payflow.notification.retry.dlq     ← terminal-failure parking lot
/// </code>
///
/// Routing key for the retry exchange is hard-coded to "schedule" so the
/// queue binds with a single key. The DLQ is unbound (publish goes through
/// the default exchange + queue name).
/// </summary>
internal static class RabbitMqTopology
{
    public const string RetryRoutingKey = "schedule";

    public static void Declare(IConnection connection, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange: options.RetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        var waitArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = options.WorkQueue,
        };
        channel.QueueDeclare(
            queue: options.WaitQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: waitArgs);
        channel.QueueBind(
            queue: options.WaitQueue,
            exchange: options.RetryExchange,
            routingKey: RetryRoutingKey);

        channel.QueueDeclare(
            queue: options.WorkQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            queue: options.DlqQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }
}
