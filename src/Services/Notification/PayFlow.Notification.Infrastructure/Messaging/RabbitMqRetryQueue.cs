using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayFlow.Notification.Application.Abstractions;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Production <see cref="INotificationRetryQueue"/>: publishes failed-send
/// retries onto the wait queue with a per-message TTL chosen by attempt
/// number, and parks DLQ messages on the dead-letter queue.
///
/// One channel per call (cheap on RabbitMQ) — we don't pool channels across
/// concurrent publishers because <see cref="IModel"/> is not thread-safe.
/// </summary>
internal sealed class RabbitMqRetryQueue : INotificationRetryQueue
{
    private readonly RabbitMqConnectionProvider _connections;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqRetryQueue> _logger;

    public RabbitMqRetryQueue(
        RabbitMqConnectionProvider connections,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqRetryQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connections = connections;
        _options = options.Value;
        _logger = logger;
    }

    public Task EnqueueAsync(Guid notificationId, int nextAttempt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var envelope = new RetryEnvelope(notificationId, nextAttempt);
        var ttl = _options.BackoffFor(nextAttempt);

        var connection = _connections.GetConnection();
        using var channel = connection.CreateModel();
        var props = channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2; // persistent
        props.MessageId = Guid.NewGuid().ToString("N");
        props.Expiration = ttl.ToString(CultureInfo.InvariantCulture);
        // Per-message TTL — message sits in the wait queue for `ttl` ms,
        // then dead-letters to the work queue where the consumer reads it.

        channel.BasicPublish(
            exchange: _options.RetryExchange,
            routingKey: RabbitMqTopology.RetryRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: envelope.ToBytes());

        _logger.LogInformation(
            "Notification {NotificationId} retry attempt {Attempt} enqueued ({TtlMs} ms backoff).",
            notificationId, nextAttempt, ttl);
        return Task.CompletedTask;
    }

    public Task ParkOnDlqAsync(Guid notificationId, string reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var envelope = new RetryEnvelope(notificationId, AttemptNumber: -1);

        var connection = _connections.GetConnection();
        using var channel = connection.CreateModel();
        var props = channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        props.MessageId = Guid.NewGuid().ToString("N");
        props.Headers = new Dictionary<string, object>
        {
            ["x-failure-reason"] = reason ?? "UNKNOWN",
        };

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.DlqQueue,
            mandatory: false,
            basicProperties: props,
            body: envelope.ToBytes());

        _logger.LogWarning(
            "Notification {NotificationId} parked on DLQ ({Reason}).",
            notificationId, reason);
        return Task.CompletedTask;
    }
}
