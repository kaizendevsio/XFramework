using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Configurations;

public sealed class StorageFileIdentifierGroupConfiguration : IEntityTypeConfiguration<StorageFileIdentifierGroup>
{
    public void Configure(EntityTypeBuilder<StorageFileIdentifierGroup> entity)
    {
        entity.HasKey(e => e.Id).HasName("scheduleentitygroup_pk");
        entity.ToTable("StorageFileIdentifierGroup", "Storage");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
        entity.Property(e => e.SystemReferenceId).HasDefaultValue(Guid.Empty);
    }
}
