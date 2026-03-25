using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_Application");

        entity.ToTable("Application", "Application");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Name).HasColumnType("character varying");
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.Property(e => e.ParentTenantId).HasColumnName("ParentAppID");
        entity.Property(e => e.Version).HasPrecision(6, 3);
    }
}
