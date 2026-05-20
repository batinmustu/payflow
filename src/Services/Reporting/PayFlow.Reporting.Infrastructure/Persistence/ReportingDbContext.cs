using Microsoft.EntityFrameworkCore;
using PayFlow.Reporting.Domain.Idempotency;
using PayFlow.Reporting.Domain.Projections;

namespace PayFlow.Reporting.Infrastructure.Persistence;

public sealed class ReportingDbContext : DbContext
{
    public const string SchemaName = "report";

    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options)
    {
    }

    public DbSet<DailyTransactionSummary> DailySummaries => Set<DailyTransactionSummary>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
    }
}
