using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AddressRegionConfiguration : IEntityTypeConfiguration<AddressRegion>
{
    public void Configure(EntityTypeBuilder<AddressRegion> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_addressregions_pk");

        entity.ToTable("AddressRegion", "GeoLocation");

        entity.HasIndex(e => e.Code, "tbl_addressregions_code_uindex").IsUnique();


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CountryId).HasColumnName("CountryID");


        entity.HasOne(d => d.Country).WithMany(p => p.AddressRegions)
            .HasForeignKey(d => d.CountryId)
            .HasConstraintName("tbl_addressregions_tbl_addresscountry_id_fk");
    }
}
