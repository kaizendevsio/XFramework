using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class StorageFileConfiguration : IEntityTypeConfiguration<StorageFile>
{
    public void Configure(EntityTypeBuilder<StorageFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("storagefile_pk");

        entity.ToTable("StorageFile", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.ContentPath).HasColumnType("character varying");
        entity.Property(e => e.ContentType).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.Hash).HasColumnType("character varying");
        entity.Property(e => e.Identifier);
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasOne(d => d.Type).WithMany(p => p.StorageFiles)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("storagefile_storagefileentity_id_fk");

        entity.HasOne(d => d.StorageFileIdentifier).WithMany(p => p.StorageFiles)
            .HasForeignKey(d => d.StorageFileIdentifierId)
            .HasConstraintName("storagefile_storagefileidentifier_id_fk");
    }
}
