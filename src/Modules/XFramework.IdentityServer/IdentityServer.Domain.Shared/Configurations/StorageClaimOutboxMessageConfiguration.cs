using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class StorageClaimOutboxMessageConfiguration : IEntityTypeConfiguration<StorageClaimOutboxMessage>
{
    public void Configure(EntityTypeBuilder<StorageClaimOutboxMessage> entity)
    {
        entity.ToTable("StorageClaimOutboxMessage", "Identity");
        entity.HasKey(message => message.Id).HasName("PK_StorageClaimOutboxMessage");
        entity.Property(message => message.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(message => message.LastError).HasMaxLength(2_000);
        entity.Property(message => message.LeaseOwner).HasMaxLength(128);
        entity.Property(message => message.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(message => message.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(message => message.IsEnabled).HasDefaultValue(true);
        entity.HasIndex(message => new { message.TenantId, message.StorageFileId, message.RequestId })
            .IsUnique()
            .HasDatabaseName("UX_StorageClaimOutbox_Tenant_File_Request");
        entity.HasIndex(message => new
            {
                message.NextAttemptAt,
                message.LeaseExpiresAt,
                message.CreatedAt
            })
            .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE")
            .HasDatabaseName("IX_StorageClaimOutbox_Global_Due");
    }
}
