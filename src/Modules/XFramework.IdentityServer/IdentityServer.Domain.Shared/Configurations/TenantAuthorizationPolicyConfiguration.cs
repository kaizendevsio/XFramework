using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class TenantAuthorizationPolicyConfiguration : IEntityTypeConfiguration<TenantAuthorizationPolicy>
{
    public void Configure(EntityTypeBuilder<TenantAuthorizationPolicy> entity)
    {
        entity.HasKey(e => e.Id).HasName("tenantauthorizationpolicy_pk");

        entity.ToTable("TenantAuthorizationPolicy", "Identity");

        entity.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_TenantAuthorizationPolicy_Tenant");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");

        entity.Property(e => e.MissingPermissionBehavior)
            .IsRequired()
            .HasDefaultValue(MissingPermissionBehavior.Deny);

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true")
            .HasSentinel(true);

        entity.HasOne(d => d.Tenant).WithOne(p => p.AuthorizationPolicy)
            .HasForeignKey<TenantAuthorizationPolicy>(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tenantauthorizationpolicy_tenant_tenantid_fk");
    }
}
