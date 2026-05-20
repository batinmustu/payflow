using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<TransactionAggregate>
{
    public void Configure(EntityTypeBuilder<TransactionAggregate> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(t => t.OrderReference)
            .HasColumnName("order_reference")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.AmountMinor).HasColumnName("amount_minor").IsRequired();
        builder.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();

        builder.Property(t => t.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(t => t.FinalProviderCode)
            .HasColumnName("final_provider_code")
            .HasMaxLength(32);

        builder.Property(t => t.ProviderReference)
            .HasColumnName("provider_reference")
            .HasMaxLength(64);

        builder.Property(t => t.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(32);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CapturedAt).HasColumnName("captured_at");
        builder.Property(t => t.FailedAt).HasColumnName("failed_at");

        builder.Property(t => t.RefundedAmountMinor)
            .HasColumnName("refunded_amount_minor")
            .HasDefaultValue(0L)
            .IsRequired();

        // Per docs/database/erd-transaction.md indices.
        builder.HasIndex(t => new { t.TenantId, t.OrderReference })
            .IsUnique()
            .HasDatabaseName("ix_transactions_tenant_order_ref");

        builder.HasIndex(t => new { t.TenantId, t.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_transactions_tenant_created_desc");

        builder.Ignore(t => t.DomainEvents);
    }
}
