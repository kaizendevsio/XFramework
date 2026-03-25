using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityRoleTypeGroupConfiguration : IEntityTypeConfiguration<IdentityRoleTypeGroup>
{
    public void Configure(EntityTypeBuilder<IdentityRoleTypeGroup> entity)
    {
        entity.HasKey(e => e.Id).HasName("identityroleentitygroup_pk");

        entity.ToTable("IdentityRoleEntityGroup", "Identity");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}
