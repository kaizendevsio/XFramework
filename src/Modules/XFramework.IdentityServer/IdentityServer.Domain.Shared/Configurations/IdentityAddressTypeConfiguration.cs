using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityAddressTypeConfiguration : IEntityTypeConfiguration<IdentityAddressType>
{
    public void Configure(EntityTypeBuilder<IdentityAddressType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddressType");

        entity.ToTable("IdentityAddressType", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.Name).HasMaxLength(500);

        entity.HasData(
            new IdentityAddressType { Id = IdentityConstants.AddressType.Home, IsEnabled = true, Name = "HOME" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Personal, IsEnabled = true, Name = "PERSONAL" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Business, IsEnabled = true, Name = "BUSINESS" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Work, IsEnabled = true, Name = "WORK" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Billing, IsEnabled = true, Name = "BILLING" },
            new IdentityAddressType { Id = IdentityConstants.AddressType.Shipping, IsEnabled = true, Name = "SHIPPING" }
        );
    }
}
