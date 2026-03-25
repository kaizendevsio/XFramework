using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AddressBarangayConfiguration : IEntityTypeConfiguration<AddressBarangay>
{
    public void Configure(EntityTypeBuilder<AddressBarangay> entity)
    {
        entity.HasKey(e => e.Id).HasName("addresses_refbrgy_pk");

        entity.ToTable("AddressBarangay", "GeoLocation");

        entity.HasIndex(e => e.Code, "addresses_refbrgy_code_uindex").IsUnique();


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


        entity.HasOne(d => d.CityCode).WithMany(p => p.AddressBarangays)
            .HasPrincipalKey(p => p.Code)
            .HasForeignKey(d => d.CityCodeId)
            .HasConstraintName("tbl_addressbarangay_tbl_addresscity_code_fk");
    }
}
