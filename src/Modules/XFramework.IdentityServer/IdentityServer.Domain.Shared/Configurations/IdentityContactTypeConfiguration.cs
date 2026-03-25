using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityContactTypeConfiguration : IEntityTypeConfiguration<IdentityContactType>
{
    public void Configure(EntityTypeBuilder<IdentityContactType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContactType");

        entity.ToTable("IdentityContactType", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasData(
            new IdentityContactType() { Id = IdentityConstants.ContactType.Phone, IsEnabled = true, IsDeleted = false, Name = "Phone" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Email, IsEnabled = true, IsDeleted = false, Name = "Email" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Facebook, IsEnabled = true, IsDeleted = false, Name = "Facebook" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Instagram, IsEnabled = true, IsDeleted = false, Name = "Instagram" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.Twitter, IsEnabled = true, IsDeleted = false, Name = "Twitter" },
            new IdentityContactType() { Id = IdentityConstants.ContactType.LinkedIn, IsEnabled = true, IsDeleted = false, Name = "LinkedIn" }
        );
    }
}
