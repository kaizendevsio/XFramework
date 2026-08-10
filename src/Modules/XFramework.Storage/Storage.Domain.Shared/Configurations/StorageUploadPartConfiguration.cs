using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageUploadPartConfiguration : IEntityTypeConfiguration<StorageUploadPart>
{
    public void Configure(EntityTypeBuilder<StorageUploadPart> entity)
    {
        entity.HasKey(e => e.Id).HasName("storageuploadpart_pk");
        entity.ToTable("StorageUploadPart", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Sha256Hash).HasMaxLength(128);
        entity.Property(e => e.ProviderPartId).HasColumnType("text");
        entity.Property(e => e.Status).HasDefaultValue(StorageUploadPartStatus.Uploaded);

        entity.HasOne(e => e.UploadSession).WithMany(e => e.Parts)
            .HasForeignKey(e => e.UploadSessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("storageuploadpart_storageuploadsession_id_fk");

        entity.HasIndex(e => new { e.UploadSessionId, e.PartNumber })
            .IsUnique()
            .HasDatabaseName("ix_storageuploadpart_session_part");
    }
}
