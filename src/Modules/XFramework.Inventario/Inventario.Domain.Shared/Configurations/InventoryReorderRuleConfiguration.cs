using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class InventoryReorderRuleConfiguration : IEntityTypeConfiguration<InventoryReorderRule>
{
    public void Configure(EntityTypeBuilder<InventoryReorderRule> entity)
    {
        entity.ToTable("InventoryReorderRule", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_InventoryReorderRule");

        entity.Property(e => e.MinimumQuantity).HasPrecision(18, 4);
        entity.Property(e => e.MaximumQuantity).HasPrecision(18, 4);
        entity.Property(e => e.ReorderPoint).HasPrecision(18, 4);
        entity.Property(e => e.ReorderQuantity).HasPrecision(18, 4);
        entity.Property(e => e.PreferredSupplier).HasMaxLength(200);
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.WarehouseId, e.LocationId });
        entity.HasIndex(e => new { e.TenantId, e.IsActive });

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryReorderRule_Product");

        entity.HasOne(e => e.ProductVariation)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryReorderRule_ProductVariation");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryReorderRule_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryReorderRule_Location");
    }
}
