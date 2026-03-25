using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoles");

        entity.ToTable("IdentityRole", "Identity");

        entity.HasIndex(e => e.TypeId, "IX_tbl_IdentityRoles_RoleTypeID");

        entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityRoles_UserCredID");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.TypeId).HasColumnName("RoleTypeID");
        entity.Property(e => e.CredentialId).HasColumnName("UserCredID");

        entity.HasOne(d => d.Type).WithMany(p => p.IdentityRoles)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_identityroles_fk_1");

        entity.HasOne(d => d.Credential).WithMany(p => p.IdentityRoles)
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_identityroles_fk");
    }
}
