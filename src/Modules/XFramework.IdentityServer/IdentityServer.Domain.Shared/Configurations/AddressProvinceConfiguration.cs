using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AddressProvinceConfiguration : IEntityTypeConfiguration<AddressProvince>
{
    public void Configure(EntityTypeBuilder<AddressProvince> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_addressprovince_pk");

        entity.ToTable("AddressProvince", "GeoLocation");

        entity.HasIndex(e => e.Code, "tbl_addressprovince_code_uindex").IsUnique();


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


        entity.HasOne(d => d.RegCode).WithMany(p => p.AddressProvinces)
            .HasPrincipalKey(p => p.Code)
            .HasForeignKey(d => d.RegCodeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_addressprovince_tbl_addressregions_code_fk");
    }
}
