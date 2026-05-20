using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.TransactionId).HasColumnName("transaction_id").IsRequired();
        builder.Property(r => r.AmountMinor).HasColumnName("amount_minor").IsRequired();
        builder.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(r => r.State)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(r => r.RequestedBy).HasColumnName("requested_by").HasMaxLength(128).IsRequired();
        builder.Property(r => r.FinalProviderCode).HasColumnName("final_provider_code").HasMaxLength(32).IsRequired();
        builder.Property(r => r.FailureReason).HasColumnName("failure_reason").HasMaxLength(64);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.FailedAt).HasColumnName("failed_at");

        builder.HasIndex(r => r.TransactionId).HasDatabaseName("ix_refunds_transaction");
        builder.HasIndex(r => new { r.TenantId, r.RequestedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_refunds_tenant_requested_desc");

        builder.Ignore(r => r.DomainEvents);
    }
}
