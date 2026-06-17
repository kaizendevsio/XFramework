using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> entity)
    {
        entity.ToTable("Reservation", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_Reservation");

        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.Status).HasDefaultValue(ReservationStatus.Active);
        entity.Property(e => e.ReferenceType).HasMaxLength(100);
        entity.Property(e => e.ReservedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.Status });
        entity.HasIndex(e => new { e.TenantId, e.ReferenceType, e.ReferenceId });
        entity.HasIndex(e => e.ExpiresAt);

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_Reservation_Product");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_Reservation_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_Reservation_Location");

        entity.HasOne(e => e.StockBalance)
            .WithMany()
            .HasForeignKey(e => e.StockBalanceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_Reservation_StockBalance");
    }
}
