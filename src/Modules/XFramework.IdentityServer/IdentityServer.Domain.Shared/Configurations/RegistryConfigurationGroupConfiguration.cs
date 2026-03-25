using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class RegistryConfigurationGroupConfiguration : IEntityTypeConfiguration<RegistryConfigurationGroup>
{
    public void Configure(EntityTypeBuilder<RegistryConfigurationGroup> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_configurationgroup_pk");

        entity.ToTable("RegistryConfigurationGroup", "Registry");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.Description).HasMaxLength(1000);

        entity.Property(e => e.Name).HasMaxLength(100);
    }
}
