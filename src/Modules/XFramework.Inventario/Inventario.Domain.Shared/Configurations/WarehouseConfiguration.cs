using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> entity)
    {
        entity.ToTable("Warehouse", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_Warehouse");

        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.AddressLine).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.Region).HasMaxLength(100);
        entity.Property(e => e.PostalCode).HasMaxLength(25);
        entity.Property(e => e.CountryCode).HasMaxLength(3);
        entity.Property(e => e.IsDefault).HasDefaultValue(false);

        entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}
