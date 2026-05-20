using Microsoft.EntityFrameworkCore;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.Reporting.Domain.Projections;

namespace PayFlow.Reporting.Infrastructure.Persistence;

internal sealed class SummaryRepository : ISummaryRepository
{
    private readonly ReportingDbContext _db;
    public SummaryRepository(ReportingDbContext db) => _db = db;

    public Task<DailyTransactionSummary?> GetAsync(
        Guid tenantId,
        DateOnly bucketDate,
        string currency,
        CancellationToken ct)
    {
        var normalised = currency.Trim().ToUpperInvariant();
        return _db.Set<DailyTransactionSummary>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Date == bucketDate && s.Currency == normalised, ct);
    }

    public async Task<IReadOnlyList<DailyTransactionSummary>> ListAsync(
        Guid tenantId,
        DateOnly fromDate,
        DateOnly toDate,
        string? currency,
        CancellationToken ct)
    {
        var query = _db.Set<DailyTransactionSummary>()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Date >= fromDate && s.Date <= toDate);

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalised = currency.Trim().ToUpperInvariant();
            query = query.Where(s => s.Currency == normalised);
        }

        return await query
            .OrderByDescending(s => s.Date)
            .ThenBy(s => s.Currency)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DailyTransactionSummary row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        await _db.Set<DailyTransactionSummary>().AddAsync(row, ct);
    }
}
