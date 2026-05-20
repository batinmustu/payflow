using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Reporting.Domain.Projections;

namespace PayFlow.Reporting.Infrastructure.Persistence.Configurations;

internal sealed class DailyTransactionSummaryConfiguration : IEntityTypeConfiguration<DailyTransactionSummary>
{
    public void Configure(EntityTypeBuilder<DailyTransactionSummary> builder)
    {
        builder.ToTable("daily_transaction_summary");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();

        builder.Property(x => x.AttemptedCount).HasColumnName("attempted_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.CapturedCount).HasColumnName("captured_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.CapturedAmountMinor).HasColumnName("captured_amount_minor").HasDefaultValue(0L).IsRequired();
        builder.Property(x => x.FailedCount).HasColumnName("failed_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.RefundedCount).HasColumnName("refunded_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.RefundedAmountMinor).HasColumnName("refunded_amount_minor").HasDefaultValue(0L).IsRequired();

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Natural key: one row per (tenant, day, currency). Used by the
        // consumers to upsert idempotently.
        builder.HasIndex(x => new { x.TenantId, x.Date, x.Currency })
            .IsUnique()
            .HasDatabaseName("ux_daily_summary_tenant_date_currency");

        builder.HasIndex(x => new { x.TenantId, x.Date })
            .IsDescending(false, true)
            .HasDatabaseName("ix_daily_summary_tenant_date_desc");
    }
}
