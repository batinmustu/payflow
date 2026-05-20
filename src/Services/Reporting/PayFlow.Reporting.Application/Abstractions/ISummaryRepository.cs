using PayFlow.Reporting.Domain.Projections;

namespace PayFlow.Reporting.Application.Abstractions;

public interface ISummaryRepository
{
    /// <summary>
    /// Lookup the per-day, per-currency bucket. The first projection of the
    /// day for a tenant returns null — callers create a fresh row via
    /// <see cref="DailyTransactionSummary.Empty"/> + <see cref="AddAsync"/>.
    /// </summary>
    Task<DailyTransactionSummary?> GetAsync(Guid tenantId, DateOnly bucketDate, string currency, CancellationToken ct);

    Task<IReadOnlyList<DailyTransactionSummary>> ListAsync(
        Guid tenantId,
        DateOnly fromDate,
        DateOnly toDate,
        string? currency,
        CancellationToken ct);

    Task AddAsync(DailyTransactionSummary row, CancellationToken ct);
}
