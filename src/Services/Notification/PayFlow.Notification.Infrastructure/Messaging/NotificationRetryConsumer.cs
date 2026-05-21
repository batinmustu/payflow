using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayFlow.Notification.Application.Notifications;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Consumes the retry work queue. Each message represents one scheduled
/// retry attempt for a previously-failed notification send. The consumer
/// re-loads the audit row and asks <see cref="NotificationDispatcher"/> to
/// re-attempt; the dispatcher itself decides whether to mark Sent, schedule
/// the next retry, or terminate on the DLQ.
///
/// Prefetch is 8: small enough that one slow notification doesn't starve the
/// others, large enough to keep the network pipe used. We always ack — even
/// on dispatcher exception — because the dispatcher already persists the
/// failure on the row + schedules the next attempt (or DLQ parks) itself.
/// A redelivery would just double-attempt and confuse the audit trail.
/// </summary>
internal sealed class NotificationRetryConsumer : BackgroundService
{
    private const ushort Prefetch = 8;

    private readonly RabbitMqConnectionProvider _connections;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationRetryConsumer> _logger;

    private IModel? _channel;

    public NotificationRetryConsumer(
        RabbitMqConnectionProvider connections,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationRetryConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connections = connections;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = _connections.GetConnection();
        _channel = connection.CreateModel();
        _channel.BasicQos(prefetchSize: 0, prefetchCount: Prefetch, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageAsync;

        _channel.BasicConsume(
            queue: _options.WorkQueue,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Notification retry consumer subscribed to {WorkQueue} (prefetch={Prefetch}).",
            _options.WorkQueue, Prefetch);

        // The connection's automatic recovery handles broker hiccups; the
        // BackgroundService just stays alive until shutdown.
        return Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs e)
    {
        var envelope = RetryEnvelope.TryParse(e.Body);
        if (envelope is null)
        {
            _logger.LogWarning(
                "Dropping unparseable retry message {DeliveryTag} ({BodyBytes} bytes).",
                e.DeliveryTag, e.Body.Length);
            _channel!.BasicAck(e.DeliveryTag, multiple: false);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
            await dispatcher.RedispatchAsync(envelope.NotificationId, CancellationToken.None);
            _channel!.BasicAck(e.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Retry consumer failed for notification {NotificationId}; ack-ing to avoid replay storm.",
                envelope.NotificationId);
            // Ack anyway — the dispatcher path is supposed to be the one that
            // schedules follow-up retries. If it threw before that happened,
            // the audit row is still Pending and a manual operator can re-
            // enqueue. Redelivering by nack would cause the same exception
            // path in a tight loop.
            try { _channel!.BasicAck(e.DeliveryTag, multiple: false); } catch { /* shutting down */ }
        }
    }

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { /* best-effort */ }
        _channel?.Dispose();
        base.Dispose();
    }
}
