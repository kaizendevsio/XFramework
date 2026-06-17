using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> entity)
    {
        entity.ToTable("InventoryLocation", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_InventoryLocation");

        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.LocationType).HasDefaultValue(InventoryLocationType.Bin);
        entity.Property(e => e.IsPickable).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.WarehouseId, e.Code }).IsUnique();
        entity.HasIndex(e => new { e.TenantId, e.ParentLocationId });

        entity.HasOne(e => e.Warehouse)
            .WithMany(e => e.Locations)
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Inventario_InventoryLocation_Warehouse");

        entity.HasOne(e => e.ParentLocation)
            .WithMany(e => e.ChildLocations)
            .HasForeignKey(e => e.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryLocation_Parent");
    }
}
