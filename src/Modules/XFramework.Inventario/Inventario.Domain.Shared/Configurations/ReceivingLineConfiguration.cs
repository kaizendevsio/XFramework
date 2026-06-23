using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ReceivingLineConfiguration : IEntityTypeConfiguration<ReceivingLine>
{
    public void Configure(EntityTypeBuilder<ReceivingLine> entity)
    {
        entity.ToTable("ReceivingLine", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ReceivingLine");

        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitCost).HasPrecision(18, 4);
        entity.Property(e => e.UnitOfMeasure).HasMaxLength(25);
        entity.Property(e => e.LotNumber).HasMaxLength(100);

        entity.HasIndex(e => new { e.TenantId, e.ReceivingDocumentId });
        entity.HasIndex(e => new { e.TenantId, e.PurchaseOrderLineId });
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.LotId });

        entity.HasOne(e => e.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(e => e.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_PurchaseOrderLine");

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_Product");

        entity.HasOne(e => e.ProductVariation)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_ProductVariation");

        entity.HasOne(e => e.Lot)
            .WithMany()
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_Lot");

        entity.HasOne(e => e.StockBalance)
            .WithMany()
            .HasForeignKey(e => e.StockBalanceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_StockBalance");

        entity.HasOne(e => e.InventoryMovement)
            .WithMany()
            .HasForeignKey(e => e.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingLine_InventoryMovement");
    }
}
