using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityFavoriteConfiguration : IEntityTypeConfiguration<IdentityFavorite>
{
    public void Configure(EntityTypeBuilder<IdentityFavorite> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_userfavorites_pk");

        entity.ToTable("IdentityFavorite", "Identity");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Data).HasMaxLength(5000);
        entity.Property(e => e.FavoriteTypeId).HasColumnName("FavoriteTypeID");

        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.FavoriteType).WithMany(p => p.IdentityFavorites)
            .HasForeignKey(d => d.FavoriteTypeId)
            .HasConstraintName("tbl_userfavorites_tbl_favoriteType_id_fk");

        entity.HasOne(d => d.Credential).WithMany(p => p.IdentityFavorites)
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("tbl_userfavorites_tbl_identitycredentials_id_fk");
    }
}
