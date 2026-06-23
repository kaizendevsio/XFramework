using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> entity)
    {
        entity.ToTable("StockBalance", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_StockBalance");

        entity.Property(e => e.OnHandQuantity).HasPrecision(18, 4);
        entity.Property(e => e.ReservedQuantity).HasPrecision(18, 4);
        entity.Property(e => e.AvailableQuantity).HasPrecision(18, 4);

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.WarehouseId, e.LocationId })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NULL AND \"LotId\" IS NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.WarehouseId, e.LocationId })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NOT NULL AND \"LotId\" IS NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.WarehouseId, e.LocationId, e.LotId })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NULL AND \"LotId\" IS NOT NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.WarehouseId, e.LocationId, e.LotId })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NOT NULL AND \"LotId\" IS NOT NULL AND \"IsDeleted\" = false");

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_StockBalance_Product");

        entity.HasOne(e => e.ProductVariation)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_StockBalance_ProductVariation");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_StockBalance_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_StockBalance_Location");

        entity.HasOne(e => e.Lot)
            .WithMany()
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_StockBalance_Lot");
    }
}
