using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityRoleTypeConfiguration : IEntityTypeConfiguration<IdentityRoleType>
{
    public void Configure(EntityTypeBuilder<IdentityRoleType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityRoleType");

        entity.ToTable("IdentityRoleType", "Identity");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.TenantId);

        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityRoleTypes)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_identityroleTypes_tbl_applications_id_fk");

        entity.HasOne(d => d.Group).WithMany(p => p.IdentityRoleTypes)
            .HasForeignKey(d => d.GroupId)
            .HasConstraintName("identityroleentity_identityroleentitygroup_id_fk");
    }
}
