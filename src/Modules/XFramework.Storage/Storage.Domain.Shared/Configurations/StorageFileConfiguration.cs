using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageFileConfiguration : IEntityTypeConfiguration<StorageFile>
{
    public void Configure(EntityTypeBuilder<StorageFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("storagefile_pk");
        entity.ToTable("StorageFile", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.ContentPath).HasColumnType("character varying");
        entity.Property(e => e.ContentType).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Hash).HasColumnType("character varying");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.Property(e => e.Status).HasConversion<int>().HasDefaultValue(StorageFileStatus.Pending);
        entity.Property(e => e.Visibility).HasConversion<int>().HasDefaultValue(StorageFileVisibility.Private);
        entity.Property(e => e.ProviderProfileName).HasMaxLength(128);
        entity.Property(e => e.BucketName).HasMaxLength(128);
        entity.Property(e => e.ObjectKey).HasMaxLength(1024);
        entity.Property(e => e.Sha256Hash).HasMaxLength(128);
        entity.Property(e => e.ETag).HasMaxLength(256);
        entity.Property(e => e.PublicUrl).HasColumnType("text");
        entity.Property(e => e.CdnBaseUrl).HasColumnType("text");
        entity.Property(e => e.ObjectDeletedAt);

        entity.HasOne(d => d.Type).WithMany(p => p.StorageFiles)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("storagefile_storagefileentity_id_fk");

        entity.HasOne(d => d.StorageFileIdentifier).WithMany(p => p.StorageFiles)
            .HasForeignKey(d => d.StorageFileIdentifierId)
            .HasConstraintName("storagefile_storagefileidentifier_id_fk");

        entity.HasOne(d => d.ProviderProfile).WithMany(p => p.Files)
            .HasForeignKey(d => d.ProviderProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("storagefile_storageproviderprofile_id_fk");

        entity.HasOne(d => d.TenantBucket).WithMany(p => p.Files)
            .HasForeignKey(d => d.TenantBucketId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("storagefile_storagetenantbucket_id_fk");

        entity.HasIndex(e => new { e.TenantId, e.Status, e.Visibility })
            .HasDatabaseName("ix_storagefile_tenant_status_visibility");
        entity.HasIndex(e => new { e.TenantId, e.RetentionUntil, e.ObjectDeletedAt })
            .HasDatabaseName("ix_storagefile_tenant_retention_objectdeleted");
        entity.HasIndex(e => new { e.TenantId, e.Identifier })
            .HasDatabaseName("ix_storagefile_tenant_identifier");
        entity.HasIndex(e => new { e.TenantId, e.BucketName, e.ObjectKey })
            .HasDatabaseName("ix_storagefile_tenant_bucket_object");
    }
}
