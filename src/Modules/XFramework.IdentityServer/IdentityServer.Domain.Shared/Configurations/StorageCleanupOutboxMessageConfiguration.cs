using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class StorageCleanupOutboxMessageConfiguration : IEntityTypeConfiguration<StorageCleanupOutboxMessage>
{
    public void Configure(EntityTypeBuilder<StorageCleanupOutboxMessage> entity)
    {
        entity.ToTable("StorageCleanupOutboxMessage", "Identity");
        entity.HasKey(message => message.Id).HasName("PK_StorageCleanupOutboxMessage");
        entity.Property(message => message.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(message => message.LastError).HasMaxLength(2_000);
        entity.Property(message => message.LeaseOwner).HasMaxLength(128);
        entity.Property(message => message.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(message => message.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(message => message.IsEnabled).HasDefaultValue(true);
        entity.HasIndex(message => new { message.TenantId, message.StorageFileId })
            .IsUnique()
            .HasDatabaseName("UX_StorageCleanupOutbox_Tenant_File");
        entity.HasIndex(message => new
            {
                message.TenantId,
                message.DeadLetteredAt,
                message.ProcessedAt,
                message.NextAttemptAt,
                message.LeaseExpiresAt
            })
            .HasDatabaseName("IX_StorageCleanupOutbox_Tenant_Due_Lease");
        entity.HasIndex(message => new
            {
                message.NextAttemptAt,
                message.LeaseExpiresAt,
                message.CreatedAt
            })
            .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE")
            .HasDatabaseName("IX_StorageCleanupOutbox_Global_Due");
    }
}
