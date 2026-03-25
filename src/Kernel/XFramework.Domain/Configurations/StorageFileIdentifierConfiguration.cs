using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class StorageFileIdentifierConfiguration : IEntityTypeConfiguration<StorageFileIdentifier>
{
    public void Configure(EntityTypeBuilder<StorageFileIdentifier> entity)
    {
        entity.HasKey(e => e.Id).HasName("storagefileidentifier_pk");

        entity.ToTable("StorageFileIdentifier", "Storage");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasOne(d => d.Group).WithMany(p => p.StorageFileIdentifiers)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("storagefileidentifier_storagefileidentifiergroup_id_fk");
    }
}
