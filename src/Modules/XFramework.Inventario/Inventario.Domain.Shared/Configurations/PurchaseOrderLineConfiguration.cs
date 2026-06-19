using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> entity)
    {
        entity.ToTable("PurchaseOrderLine", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_PurchaseOrderLine");

        entity.Property(e => e.OrderedQuantity).HasPrecision(18, 4);
        entity.Property(e => e.ReceivedQuantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitCost).HasPrecision(18, 4);
        entity.Property(e => e.UnitOfMeasure).HasMaxLength(25);
        entity.Property(e => e.Notes).HasMaxLength(1000);

        entity.HasIndex(e => new { e.TenantId, e.PurchaseOrderId });
        entity.HasIndex(e => new { e.TenantId, e.ProductId });

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_PurchaseOrderLine_Product");
    }
}
