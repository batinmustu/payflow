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
/// HA-safe: row claim uses <c>SELECT … FOR UPDATE SKIP LOCKED</c> inside a
/// short transaction, so multiple instances of the worker can run side by
/// side without re-publishing the same row. A periodic sweep resets rows
/// stuck in <c>Publishing</c> back to <c>Pending</c> (in case a previous
/// instance crashed between marking and publishing).
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
        ArgumentNullException.ThrowIfNull(options);
        _services = services;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepStuckPublishingAsync(stoppingToken);
        var lastSweep = DateTimeOffset.UtcNow;
        var sweepInterval = TimeSpan.FromSeconds(_options.StuckSweepIntervalSeconds);

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

            if (DateTimeOffset.UtcNow - lastSweep >= sweepInterval)
            {
                try { await SweepStuckPublishingAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Outbox stuck-sweep failed"); }
                lastSweep = DateTimeOffset.UtcNow;
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
        // 1) Atomic claim: SELECT … FOR UPDATE SKIP LOCKED + state=Publishing,
        // all inside one short transaction so other workers don't see these
        // rows until we've committed.
        List<Guid> claimedIds;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var schema = db.Model.GetDefaultSchema() ?? "public";
            var now = DateTimeOffset.UtcNow;
            var batchSize = _options.BatchSize;

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Schema is derived from the context's HasDefaultSchema, not from
            // request data — safe to interpolate into the SQL string. {0} and
            // {1} stay as FromSqlRaw parameter placeholders.
            var sql =
                $"SELECT * FROM \"{schema}\".\"outbox_messages\" " +
                "WHERE state IN ('Pending', 'Failed') AND next_attempt_at <= {0} " +
                "ORDER BY created_at LIMIT {1} FOR UPDATE SKIP LOCKED";

            var batch = await db.Set<OutboxMessage>()
                .FromSqlRaw(sql, now, batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            foreach (var msg in batch)
            {
                msg.MarkPublishing();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            claimedIds = batch.Select(b => b.Id).ToList();
        }

        // 2) Publish + finalise the state. Separate scope/context so the
        // earlier transaction (and its row locks) are fully released while
        // we make the network call. Other workers won't pick these rows up
        // because their state is already Publishing.
        int processed = 0;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var rows = await db.Set<OutboxMessage>()
                .Where(m => claimedIds.Contains(m.Id))
                .ToListAsync(ct);

            foreach (var msg in rows)
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
                processed++;
            }

            await db.SaveChangesAsync(ct);
        }

        return processed;
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
        _logger.LogInformation(
            "Recovered {Count} stuck outbox row(s) from Publishing → Pending", stuck.Count);
    }
}
