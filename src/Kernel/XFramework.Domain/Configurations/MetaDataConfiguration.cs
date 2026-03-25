using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class MetaDataConfiguration : IEntityTypeConfiguration<MetaData>
{
    public void Configure(EntityTypeBuilder<MetaData> entity)
    {
        entity.HasKey(e => e.Id).HasName("metadata_pk");

        entity.ToTable("MetaData", "MetaData");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Value).HasColumnType("character varying");

        entity.HasOne(d => d.Type).WithMany(p => p.MetaData)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("metadata_metadataentity_id_fk");
    }
}
