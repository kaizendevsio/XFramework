using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class IdentityRoleFeaturePermissionOverrideConfiguration :
    IEntityTypeConfiguration<IdentityRoleFeaturePermissionOverride>
{
    public void Configure(EntityTypeBuilder<IdentityRoleFeaturePermissionOverride> entity)
    {
        entity.HasKey(e => e.Id).HasName("identityrolefeaturepermissionoverride_pk");

        entity.ToTable("IdentityRoleFeaturePermissionOverride", "Identity");

        entity.HasIndex(e => new { e.TenantId, e.IdentityRoleId, e.ModuleKey, e.SubFeatureKey, e.CapabilityKey })
            .IsUnique()
            .HasDatabaseName("IX_IdentityRoleFeaturePermissionOverride_Role_Feature_Capability");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");

        entity.Property(e => e.ModuleKey)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("character varying");

        entity.Property(e => e.SubFeatureKey)
            .IsRequired()
            .HasMaxLength(128)
            .HasDefaultValue(string.Empty)
            .HasColumnType("character varying");

        entity.Property(e => e.CapabilityKey)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying");

        entity.Property(e => e.Effect)
            .IsRequired()
            .HasDefaultValue(RoleCapabilityPermissionEffect.Allow)
            .HasSentinel(RoleCapabilityPermissionEffect.Allow);

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true")
            .HasSentinel(true);

        entity.HasOne(d => d.IdentityRole).WithMany(p => p.PermissionOverrides)
            .HasForeignKey(d => d.IdentityRoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("identityrolefeaturepermissionoverride_identityrole_fk");
    }
}
