using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageTenantBucketConfiguration : IEntityTypeConfiguration<StorageTenantBucket>
{
    public void Configure(EntityTypeBuilder<StorageTenantBucket> entity)
    {
        entity.HasKey(e => e.Id).HasName("storagetenantbucket_pk");
        entity.ToTable("StorageTenantBucket", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.BucketName).HasMaxLength(128);
        entity.Property(e => e.PublicBaseUrl).HasColumnType("text");
        entity.Property(e => e.CdnBaseUrl).HasColumnType("text");
        entity.Property(e => e.Purpose).HasDefaultValue(StorageBucketPurpose.Private);

        entity.HasOne(e => e.ProviderProfile).WithMany(e => e.TenantBuckets)
            .HasForeignKey(e => e.ProviderProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("storagetenantbucket_storageproviderprofile_id_fk");

        entity.HasIndex(e => new { e.TenantId, e.ProviderProfileId, e.Purpose })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("ix_storagetenantbucket_tenant_provider");
        entity.HasIndex(e => e.BucketName)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("ix_storagetenantbucket_bucket");
    }
}
