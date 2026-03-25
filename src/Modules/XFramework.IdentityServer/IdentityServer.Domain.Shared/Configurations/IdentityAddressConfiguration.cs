using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityAddressConfiguration : IEntityTypeConfiguration<IdentityAddress>
{
    public void Configure(EntityTypeBuilder<IdentityAddress> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAddresses");

        entity.ToTable("IdentityAddress", "Identity");

        entity.HasIndex(e => e.AddressTypeId, "IX_tbl_IdentityAddresses_AddressTypeID");

        entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityAddresses_UserInfoID");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.AddressTypeId).HasColumnName("AddressTypeID");
        entity.Property(e => e.Building).HasMaxLength(500);

        entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
        entity.Property(e => e.Street).HasMaxLength(500);
        entity.Property(e => e.Subdivision).HasMaxLength(500);
        entity.Property(e => e.UnitNumber).HasMaxLength(500);

        entity.HasOne(d => d.AddressType).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("AddressTypeID");

        entity.HasOne(d => d.Barangay).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.BarangayId)
            .HasConstraintName("tbl_identityaddresses__id_fk_brgy");

        entity.HasOne(d => d.City).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.CityId)
            .HasConstraintName("tbl_identityaddresses__id_fk_city");

        entity.HasOne(d => d.Country).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.CountryId)
            .HasConstraintName("tbl_identityaddresses_tbl_addresscountry__fk");

        entity.HasOne(d => d.IdentityInfo).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.IdentityInfoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("UserInfoID");

        entity.HasOne(d => d.Province).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.ProvinceId)
            .HasConstraintName("tbl_identityaddresses__id_fk_province");

        entity.HasOne(d => d.Region).WithMany(p => p.IdentityAddresses)
            .HasForeignKey(d => d.RegionId)
            .HasConstraintName("tbl_identityaddresses__id_fk");
    }
}
