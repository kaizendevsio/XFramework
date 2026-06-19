using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ReceivingDocumentConfiguration : IEntityTypeConfiguration<ReceivingDocument>
{
    public void Configure(EntityTypeBuilder<ReceivingDocument> entity)
    {
        entity.ToTable("ReceivingDocument", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ReceivingDocument");

        entity.Property(e => e.ReceiptNumber).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Status).HasDefaultValue(ReceivingDocumentStatus.Posted);
        entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
        entity.Property(e => e.RequestHash).HasMaxLength(128);

        entity.HasIndex(e => new { e.TenantId, e.ReceiptNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.PurchaseOrderId });
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        entity.HasOne(e => e.PurchaseOrder)
            .WithMany()
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingDocument_PurchaseOrder");

        entity.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingDocument_Warehouse");

        entity.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingDocument_Location");

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ReceivingDocument_Supplier");

        entity.HasMany(e => e.Lines)
            .WithOne(e => e.ReceivingDocument)
            .HasForeignKey(e => e.ReceivingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
