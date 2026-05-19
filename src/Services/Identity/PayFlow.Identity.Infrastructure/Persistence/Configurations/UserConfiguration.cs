using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Identity.Domain.Users;

namespace PayFlow.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();

        // Email is a value object — we store the underlying string and
        // reconstruct the VO when reading. Email.Create throws no
        // exception on rehydration because stored values are already
        // normalised; we use a converter that bypasses re-validation.
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .HasConversion(
                vo => vo.Value,
                raw => Email.Create(raw).Value)
            .IsRequired();

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();

        // Roles are a simple owned list, persisted as a Postgres text[].
        builder.Property<List<string>>("_roles")
            .HasColumnName("roles")
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("text[]")
            .IsRequired();
        builder.Ignore(u => u.Roles);

        // String-based property names so the value-converted Email column
        // participates in the index without the converter getting in the way.
        builder.HasIndex(nameof(User.TenantId), nameof(User.Email))
            .IsUnique()
            .HasDatabaseName("ix_users_tenant_email");

        builder.Ignore(u => u.DomainEvents);
    }
}
