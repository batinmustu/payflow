namespace PayFlow.Reporting.Domain.Projections;

/// <summary>
/// Per-tenant, per-day, per-currency projection of the headline transaction
/// metrics. Rebuilt from the Kafka event stream — never written by request
/// path code, only by the integration-event consumers in
/// <c>PayFlow.Reporting.Application</c>. Read by the dashboard endpoints.
/// </summary>
public sealed class DailyTransactionSummary
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    public int AttemptedCount { get; private set; }
    public int CapturedCount { get; private set; }
    public long CapturedAmountMinor { get; private set; }
    public int FailedCount { get; private set; }
    public int RefundedCount { get; private set; }
    public long RefundedAmountMinor { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private DailyTransactionSummary() { }

    public static DailyTransactionSummary Empty(Guid tenantId, DateOnly date, string currency) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Date = date,
            Currency = currency.Trim().ToUpperInvariant(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    public void RecordAttempted() { AttemptedCount++; Touch(); }

    public void RecordCaptured(long amountMinor)
    {
        CapturedCount++;
        CapturedAmountMinor += amountMinor;
        Touch();
    }

    public void RecordFailed() { FailedCount++; Touch(); }

    public void RecordRefunded(long amountMinor)
    {
        RefundedCount++;
        RefundedAmountMinor += amountMinor;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
