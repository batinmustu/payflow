using PayFlow.Reporting.Domain.Idempotency;

namespace PayFlow.Reporting.Application.Abstractions;

public interface IProcessedEventStore
{
    Task<bool> WasProcessedAsync(Guid messageId, CancellationToken ct);
    Task AddAsync(ProcessedEvent marker, CancellationToken ct);
}
