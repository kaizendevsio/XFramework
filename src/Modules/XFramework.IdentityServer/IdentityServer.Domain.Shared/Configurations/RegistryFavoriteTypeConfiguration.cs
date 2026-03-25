using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class RegistryFavoriteTypeConfiguration : IEntityTypeConfiguration<RegistryFavoriteType>
{
    public void Configure(EntityTypeBuilder<RegistryFavoriteType> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_favoriteType_pk");

        entity.ToTable("RegistryFavoriteType", "Registry");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
        entity.Property(e => e.Name).HasMaxLength(100);
    }
}
