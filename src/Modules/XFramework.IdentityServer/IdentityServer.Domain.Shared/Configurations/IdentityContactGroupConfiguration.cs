using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityContactGroupConfiguration : IEntityTypeConfiguration<IdentityContactGroup>
{
    public void Configure(EntityTypeBuilder<IdentityContactGroup> entity)
    {
        entity.HasKey(e => e.Id).HasName("identitycontactgroup_pk");

        entity.ToTable("IdentityContactGroup", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasData(
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Home, IsEnabled = true, Name = "HOME" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Personal, IsEnabled = true, Name = "PERSONAL" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Business, IsEnabled = true, Name = "BUSINESS" },
            new IdentityContactGroup { Id = IdentityConstants.ContactGroup.Work, IsEnabled = true, Name = "WORK" }
        );
    }
}
