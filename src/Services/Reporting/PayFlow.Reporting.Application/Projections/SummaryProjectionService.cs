using Microsoft.Extensions.Logging;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.Reporting.Domain.Idempotency;
using PayFlow.Reporting.Domain.Projections;
using PayFlow.SharedKernel;

namespace PayFlow.Reporting.Application.Projections;

/// <summary>
/// The shared upsert path every event consumer goes through. Takes care of
/// idempotency (skip if message_id already processed) and bucketing (per
/// tenant, per UTC date, per currency). Consumers just declare which counter
/// to bump via <paramref name="apply"/>.
/// </summary>
public sealed class SummaryProjectionService
{
    private readonly ISummaryRepository _summaries;
    private readonly IProcessedEventStore _processed;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SummaryProjectionService> _logger;

    public SummaryProjectionService(
        ISummaryRepository summaries,
        IProcessedEventStore processed,
        IUnitOfWork uow,
        ILogger<SummaryProjectionService> logger)
    {
        _summaries = summaries;
        _processed = processed;
        _uow = uow;
        _logger = logger;
    }

    public async Task ApplyAsync(
        Guid messageId,
        string eventType,
        Guid tenantId,
        DateTimeOffset occurredAt,
        string currency,
        Action<DailyTransactionSummary> apply,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(apply);

        if (await _processed.WasProcessedAsync(messageId, ct))
        {
            _logger.LogInformation(
                "Skipping {EventType} message {MessageId} — already projected.",
                eventType, messageId);
            return;
        }

        try
        {
            await ApplyOnceAsync(messageId, eventType, tenantId, occurredAt, currency, apply, ct);
        }
        catch (UniqueConstraintViolationException ex) when (
            ex.ConstraintName is "ux_daily_summary_tenant_date_currency" or "PK_processed_events")
        {
            // Two consumers raced on the same bucket (or the same message
            // arrived twice while neither attempt had written processed_events
            // yet). Reload the existing row and replay our delta on top of
            // it — the Kafka retry loop relies on this being safe.
            _logger.LogInformation(
                "Projection race on {EventType} {MessageId} ({Constraint}); replaying onto existing row.",
                eventType, messageId, ex.ConstraintName);
            await ApplyOnceAsync(messageId, eventType, tenantId, occurredAt, currency, apply, ct);
        }
    }

    private async Task ApplyOnceAsync(
        Guid messageId,
        string eventType,
        Guid tenantId,
        DateTimeOffset occurredAt,
        string currency,
        Action<DailyTransactionSummary> apply,
        CancellationToken ct)
    {
        // Re-check dedup inside the retry path — the conflict may have been
        // a duplicate message_id that just landed in processed_events.
        if (await _processed.WasProcessedAsync(messageId, ct))
        {
            return;
        }

        var bucketDate = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        var row = await _summaries.GetAsync(tenantId, bucketDate, currency, ct);
        if (row is null)
        {
            row = DailyTransactionSummary.Empty(tenantId, bucketDate, currency);
            await _summaries.AddAsync(row, ct);
        }

        apply(row);
        await _processed.AddAsync(ProcessedEvent.Mark(messageId, eventType), ct);
        await _uow.SaveChangesAsync(ct);
    }
}
