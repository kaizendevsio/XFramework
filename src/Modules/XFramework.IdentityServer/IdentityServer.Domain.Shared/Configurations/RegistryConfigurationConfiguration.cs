using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class RegistryConfigurationConfiguration : IEntityTypeConfiguration<RegistryConfiguration>
{
    public void Configure(EntityTypeBuilder<RegistryConfiguration> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_applicationconfiguration_pk");

        entity.ToTable("RegistryConfiguration", "Registry");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.TenantId);

        entity.Property(e => e.Key).HasColumnType("character varying");
        entity.Property(e => e.Unit).HasMaxLength(100);
        entity.Property(e => e.Value).HasColumnType("character varying");

        entity.HasOne(d => d.Tenant).WithMany(p => p.RegistryConfigurations)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_applicationconfiguration_tbl_application_id_fk");

        entity.HasOne(d => d.Group).WithMany(p => p.RegistryConfigurations)
            .HasForeignKey(d => d.GroupId)
            .HasConstraintName("tbl_configurations_tbl_configurationgroup_id_fk");
    }
}
