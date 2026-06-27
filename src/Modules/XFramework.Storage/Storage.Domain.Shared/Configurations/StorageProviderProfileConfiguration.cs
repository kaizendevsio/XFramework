using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageProviderProfileConfiguration : IEntityTypeConfiguration<StorageProviderProfile>
{
    public void Configure(EntityTypeBuilder<StorageProviderProfile> entity)
    {
        entity.HasKey(e => e.Id).HasName("storageproviderprofile_pk");
        entity.ToTable("StorageProviderProfile", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Name).HasMaxLength(128);
        entity.Property(e => e.Kind).HasConversion<int>();
        entity.Property(e => e.Endpoint).HasColumnType("text");
        entity.Property(e => e.Region).HasMaxLength(128);
        entity.Property(e => e.AccessKeyId).HasColumnType("text");
        entity.Property(e => e.SecretAccessKey).HasColumnType("text");
        entity.Property(e => e.ConnectionString).HasColumnType("text");
        entity.Property(e => e.AccessKeyIdSecretName).HasMaxLength(256);
        entity.Property(e => e.SecretAccessKeySecretName).HasMaxLength(256);
        entity.Property(e => e.ConnectionStringSecretName).HasMaxLength(256);
        entity.Property(e => e.BucketPrefix).HasMaxLength(64);
        entity.Property(e => e.PublicBaseUrl).HasColumnType("text");
        entity.Property(e => e.CdnBaseUrl).HasColumnType("text");

        entity.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("ix_storageproviderprofile_tenant_name");
        entity.HasIndex(e => new { e.TenantId, e.IsDefault })
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false")
            .HasDatabaseName("ix_storageproviderprofile_tenant_default");
    }
}
