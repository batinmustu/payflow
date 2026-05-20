using Microsoft.EntityFrameworkCore;
using PayFlow.Outbox;
using PayFlow.Transaction.Domain.Refunds;
using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContext : DbContext
{
    public const string SchemaName = "transaction";

    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options)
    {
    }

    public DbSet<TransactionAggregate> Transactions => Set<TransactionAggregate>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
