using Microsoft.EntityFrameworkCore;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.Reporting.Domain.Idempotency;

namespace PayFlow.Reporting.Infrastructure.Persistence;

internal sealed class ProcessedEventStore : IProcessedEventStore
{
    private readonly ReportingDbContext _db;
    public ProcessedEventStore(ReportingDbContext db) => _db = db;

    public Task<bool> WasProcessedAsync(Guid messageId, CancellationToken ct) =>
        _db.Set<ProcessedEvent>().AnyAsync(p => p.MessageId == messageId, ct);

    public async Task AddAsync(ProcessedEvent marker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(marker);
        await _db.Set<ProcessedEvent>().AddAsync(marker, ct);
    }
}
