using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> entity)
    {
        entity.ToTable("InventoryLot", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_InventoryLot");

        entity.Property(e => e.LotNumber).HasMaxLength(100).IsRequired();
        entity.Property(e => e.SupplierReference).HasMaxLength(200);
        entity.Property(e => e.SourceReferenceType).HasMaxLength(100);
        entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.UnitCost).HasPrecision(18, 4);
        entity.Property(e => e.Status).HasDefaultValue(InventoryLotStatus.Available);

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.LotNumber })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.LotNumber })
            .IsUnique()
            .HasFilter("\"ProductVariationId\" IS NOT NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId, e.ExpiresAt });
        entity.HasIndex(e => new { e.TenantId, e.Status });

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryLot_Product");

        entity.HasOne(e => e.ProductVariation)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_InventoryLot_ProductVariation");
    }
}
