using Microsoft.EntityFrameworkCore;
using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContext : DbContext
{
    public const string SchemaName = "transaction";

    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options)
    {
    }

    public DbSet<TransactionAggregate> Transactions => Set<TransactionAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionDbContext).Assembly);
    }
}
