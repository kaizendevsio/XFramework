using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ReservationAllocationConfiguration : IEntityTypeConfiguration<ReservationAllocation>
{
    public void Configure(EntityTypeBuilder<ReservationAllocation> entity)
    {
        entity.ToTable("ReservationAllocation", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ReservationAllocation");

        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.Status).HasDefaultValue(ReservationAllocationStatus.Reserved);
        entity.Property(e => e.ReservedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ExpiredLotOverrideReason).HasMaxLength(500);

        entity.HasIndex(e => new { e.TenantId, e.ReservationId, e.Status });
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.WarehouseId, e.LocationId, e.LotId });
        entity.HasIndex(e => new { e.TenantId, e.LotId, e.ProductVariationId, e.Status });

        entity.HasOne(e => e.Reservation)
            .WithMany(e => e.Allocations)
            .HasForeignKey(e => e.ReservationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_Reservation");

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_Product");

        entity.HasOne(e => e.ProductVariation)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_ProductVariation");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_Location");

        entity.HasOne(e => e.StockBalance)
            .WithMany()
            .HasForeignKey(e => e.StockBalanceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_StockBalance");

        entity.HasOne(e => e.Lot)
            .WithMany()
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReservationAllocation_Lot");
    }
}
