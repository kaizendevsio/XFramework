using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class TenantModuleFeatureConfiguration : IEntityTypeConfiguration<TenantModuleFeature>
{
    public void Configure(EntityTypeBuilder<TenantModuleFeature> entity)
    {
        entity.HasKey(e => e.Id).HasName("tenantmodulefeature_pk");

        entity.ToTable("TenantModuleFeature", "Identity");

        entity.HasIndex(e => new { e.TenantId, e.ModuleKey, e.SubFeatureKey })
            .IsUnique()
            .HasDatabaseName("IX_TenantModuleFeature_Tenant_Module_SubFeature");

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

        entity.Property(e => e.DisplayName)
            .HasMaxLength(200)
            .HasColumnType("character varying");

        entity.Property(e => e.Description)
            .HasColumnType("character varying");

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true")
            .HasSentinel(true);

        entity.HasOne(d => d.Tenant).WithMany(p => p.TenantModuleFeatures)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tenantmodulefeature_application_tenantid_fk");
    }
}
