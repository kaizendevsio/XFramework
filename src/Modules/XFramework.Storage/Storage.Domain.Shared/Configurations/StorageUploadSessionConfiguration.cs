using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageUploadSessionConfiguration : IEntityTypeConfiguration<StorageUploadSession>
{
    public void Configure(EntityTypeBuilder<StorageUploadSession> entity)
    {
        entity.HasKey(e => e.Id).HasName("storageuploadsession_pk");
        entity.ToTable("StorageUploadSession", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.UploadId).HasMaxLength(128);
        entity.Property(e => e.ProviderUploadId).HasColumnType("text");
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.ExpectedSha256Hash).HasMaxLength(128);

        entity.HasOne(e => e.StorageFile).WithMany(e => e.UploadSessions)
            .HasForeignKey(e => e.StorageFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("storageuploadsession_storagefile_id_fk");

        entity.HasIndex(e => e.UploadId)
            .IsUnique()
            .HasDatabaseName("ix_storageuploadsession_uploadid");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.ExpiresAt })
            .HasDatabaseName("ix_storageuploadsession_tenant_status_expires");
    }
}
