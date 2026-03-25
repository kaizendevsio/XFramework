using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AddressCountryConfiguration : IEntityTypeConfiguration<AddressCountry>
{
    public void Configure(EntityTypeBuilder<AddressCountry> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_AddressCountry_pkey");

        entity.ToTable("AddressCountry", "GeoLocation");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CurrencyId).HasColumnName("CurrencyID");

        entity.Property(e => e.IsoCode2).HasMaxLength(2);
        entity.Property(e => e.IsoCode3).HasMaxLength(3);
        entity.Property(e => e.Language).HasMaxLength(50);
        entity.Property(e => e.Name).HasMaxLength(50);
        entity.Property(e => e.PhoneCountryCode).HasMaxLength(9);

        // FK to CurrencyType (Wallets module) — configured via Wallets.Domain.Shared configurations
        // The CurrencyId column exists; the relationship is established when Wallets assembly is loaded.
    }
}
