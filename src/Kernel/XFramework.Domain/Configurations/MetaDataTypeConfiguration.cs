using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class MetaDataTypeConfiguration : IEntityTypeConfiguration<MetaDataType>
{
    public void Configure(EntityTypeBuilder<MetaDataType> entity)
    {
        entity.HasKey(e => e.Id).HasName("metadataentity_pk");

        entity.ToTable("MetaDataType", "MetaData");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasOne(d => d.Group).WithMany(p => p.MetaDataTypes)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("metadataentity_metadataentitygroup_id_fk");
    }
}
