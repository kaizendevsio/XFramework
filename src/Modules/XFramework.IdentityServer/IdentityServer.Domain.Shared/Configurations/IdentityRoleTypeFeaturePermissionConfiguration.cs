using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class IdentityRoleTypeFeaturePermissionConfiguration :
    IEntityTypeConfiguration<IdentityRoleTypeFeaturePermission>
{
    public void Configure(EntityTypeBuilder<IdentityRoleTypeFeaturePermission> entity)
    {
        entity.HasKey(e => e.Id).HasName("identityroletypefeaturepermission_pk");

        entity.ToTable("IdentityRoleTypeFeaturePermission", "Identity");

        entity.HasIndex(e => new { e.TenantId, e.RoleTypeId, e.ModuleKey, e.SubFeatureKey, e.CapabilityKey })
            .IsUnique()
            .HasDatabaseName("IX_IdentityRoleTypeFeaturePermission_Role_Feature_Capability");

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

        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true")
            .HasSentinel(true);

        entity.HasOne(d => d.RoleType).WithMany(p => p.FeaturePermissions)
            .HasForeignKey(d => d.RoleTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("identityroletypefeaturepermission_roletype_fk");
    }
}
