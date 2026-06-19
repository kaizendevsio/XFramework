using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> entity)
    {
        entity.ToTable("InventoryMovement", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_InventoryMovement");

        entity.Property(e => e.QuantityDelta).HasPrecision(18, 4);
        entity.Property(e => e.QuantityBefore).HasPrecision(18, 4);
        entity.Property(e => e.QuantityAfter).HasPrecision(18, 4);
        entity.Property(e => e.MovementDate).HasDefaultValueSql("now()");
        entity.Property(e => e.UnitOfMeasure).HasMaxLength(25);
        entity.Property(e => e.ReferenceType).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(1000);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
        entity.Property(e => e.RequestHash).HasMaxLength(128);

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.MovementDate });
        entity.HasIndex(e => new { e.TenantId, e.LotId, e.MovementDate });
        entity.HasIndex(e => new { e.TenantId, e.ReferenceType, e.ReferenceId });
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryMovement_Product");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryMovement_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryMovement_Location");

        entity.HasOne(e => e.StockBalance)
            .WithMany()
            .HasForeignKey(e => e.StockBalanceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryMovement_StockBalance");

        entity.HasOne(e => e.Lot)
            .WithMany()
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryMovement_Lot");
    }
}
