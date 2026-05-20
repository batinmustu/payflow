using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Infrastructure.Persistence.Configurations;

internal sealed class RefundSagaConfiguration : IEntityTypeConfiguration<RefundSaga>
{
    public void Configure(EntityTypeBuilder<RefundSaga> builder)
    {
        builder.ToTable("refund_sagas");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.RefundId).HasColumnName("refund_id").IsRequired();
        builder.Property(r => r.TransactionId).HasColumnName("transaction_id").IsRequired();
        builder.Property(r => r.AmountMinor).HasColumnName("amount_minor").IsRequired();
        builder.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(r => r.ProviderCode).HasColumnName("provider_code").HasMaxLength(32).IsRequired();
        builder.Property(r => r.ProviderReference).HasColumnName("provider_reference").HasMaxLength(64).IsRequired();
        builder.Property(r => r.State)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(r => r.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(r => r.ProviderRefundReference).HasColumnName("provider_refund_reference").HasMaxLength(64);
        builder.Property(r => r.FailureReason).HasColumnName("failure_reason").HasMaxLength(64);
        builder.Property(r => r.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.FailedAt).HasColumnName("failed_at");

        // (tenant_id, refund_id) is the natural dedup key for an at-least-
        // once delivered RefundRequested. Application-level check happens
        // first; the DB unique guard catches the race.
        builder.HasIndex(r => new { r.TenantId, r.RefundId })
            .IsUnique()
            .HasDatabaseName("ux_refund_sagas_tenant_refund");

        builder.HasIndex(r => new { r.TenantId, r.StartedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_refund_sagas_tenant_started_desc");

        builder.Ignore(r => r.DomainEvents);
    }
}
