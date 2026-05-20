using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PayFlow.Outbox;

/// <summary>
/// Polls the outbox in <typeparamref name="TContext"/>, hands each row to
/// <see cref="IOutboxPublisher"/>, and walks rows through the state machine
/// in docs/database/outbox.md.
///
/// Consuming services host the service by calling
/// <see cref="OutboxServiceCollectionExtensions.AddPayFlowOutbox{T}"/> in
/// their DI setup. One service hosts one publisher worker per process.
/// </summary>
public sealed class OutboxBackgroundService<TContext> : BackgroundService
    where TContext : DbContext
{
    private readonly IServiceProvider _services;
    private readonly IOutboxPublisher _publisher;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxBackgroundService<TContext>> _logger;

    public OutboxBackgroundService(
        IServiceProvider services,
        IOutboxPublisher publisher,
        IOptions<OutboxOptions> options,
        ILogger<OutboxBackgroundService<TContext>> logger)
    {
        _services = services;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepStuckPublishingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox tick failed unexpectedly");
                processed = 0;
            }

            if (processed == 0)
            {
                try
                {
                    await Task.Delay(_options.PollIntervalMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var now = DateTimeOffset.UtcNow;
        var set = db.Set<OutboxMessage>();

        var batch = await set
            .Where(m => (m.State == OutboxMessageState.Pending || m.State == OutboxMessageState.Failed)
                       && m.NextAttemptAt <= now)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return 0;
        }

        foreach (var msg in batch)
        {
            msg.MarkPublishing();
        }
        await db.SaveChangesAsync(ct);

        foreach (var msg in batch)
        {
            try
            {
                await _publisher.PublishAsync(msg, ct);
                msg.MarkPublished();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var delay = OutboxBackoff.NextDelayFor(msg.AttemptCount);
                msg.MarkFailed(ex.Message, delay);
                _logger.LogWarning(ex,
                    "Outbox publish failed for {EventType} (attempt {Attempt})",
                    msg.EventType, msg.AttemptCount);
            }
        }

        await db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private async Task SweepStuckPublishingAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var set = db.Set<OutboxMessage>();

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.StuckPublishingThresholdSeconds);
        var stuck = await set
            .Where(m => m.State == OutboxMessageState.Publishing && m.CreatedAt <= cutoff)
            .ToListAsync(ct);

        if (stuck.Count == 0) return;

        foreach (var msg in stuck)
        {
            msg.ResetPublishingToPending();
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Recovered {Count} stuck outbox row(s) from Publishing → Pending on startup", stuck.Count);
    }
}
