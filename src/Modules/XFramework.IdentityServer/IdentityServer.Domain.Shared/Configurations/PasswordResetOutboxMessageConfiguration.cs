using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class PasswordResetOutboxMessageConfiguration : IEntityTypeConfiguration<PasswordResetOutboxMessage>
{
    public void Configure(EntityTypeBuilder<PasswordResetOutboxMessage> entity)
    {
        entity.ToTable("PasswordResetOutboxMessage", "Identity");
        entity.HasKey(message => message.Id).HasName("PK_PasswordResetOutboxMessage");

        entity.Property(message => message.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(message => message.Email).HasMaxLength(320);
        entity.Property(message => message.Phone).HasMaxLength(64);
        entity.Property(message => message.LastError).HasMaxLength(2_000);
        entity.Property(message => message.LeaseOwner).HasMaxLength(128);
        entity.Property(message => message.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(message => message.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(message => message.IsEnabled).HasDefaultValue(true);

        entity.HasIndex(message => new
            {
                message.TenantId,
                message.DeadLetteredAt,
                message.ProcessedAt,
                message.NextAttemptAt,
                message.LeaseExpiresAt
            })
            .HasDatabaseName("IX_PasswordResetOutbox_Tenant_Due_Lease");
        entity.HasIndex(message => new { message.TenantId, message.RequestId })
            .IsUnique()
            .HasDatabaseName("UX_PasswordResetOutbox_Tenant_Request");
        entity.HasIndex(message => new
            {
                message.NextAttemptAt,
                message.LeaseExpiresAt,
                message.CreatedAt
            })
            .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE")
            .HasDatabaseName("IX_PasswordResetOutbox_Global_Due");
    }
}
