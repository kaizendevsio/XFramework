using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> entity)
    {
        entity.ToTable("PurchaseOrder", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_PurchaseOrder");

        entity.Property(e => e.OrderNumber).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Status).HasDefaultValue(PurchaseOrderStatus.Draft);
        entity.Property(e => e.OrderDate).HasDefaultValueSql("now()");
        entity.Property(e => e.Notes).HasMaxLength(1000);

        entity.HasIndex(e => new { e.TenantId, e.OrderNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.Status });

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_PurchaseOrder_Supplier");

        entity.HasMany(e => e.Lines)
            .WithOne(e => e.PurchaseOrder)
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
