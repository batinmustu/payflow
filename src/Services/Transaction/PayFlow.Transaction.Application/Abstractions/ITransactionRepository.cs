using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Application.Abstractions;

public interface ITransactionRepository
{
    Task<bool> OrderReferenceExistsAsync(Guid tenantId, string orderReference, CancellationToken ct);
    Task<TransactionAggregate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task AddAsync(TransactionAggregate transaction, CancellationToken ct);
}
