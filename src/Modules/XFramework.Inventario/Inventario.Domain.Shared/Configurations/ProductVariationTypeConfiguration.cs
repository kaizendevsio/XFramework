using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ProductVariationTypeConfiguration : IEntityTypeConfiguration<ProductVariationType>
{
    public void Configure(EntityTypeBuilder<ProductVariationType> entity)
    {
        entity.ToTable("ProductVariationType", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ProductVariationType");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(120);
        entity.Property(e => e.Code).HasMaxLength(50);

        entity.HasIndex(e => new { e.TenantId, e.NormalizedName })
            .IsUnique()
            .HasFilter("\"ProductId\" IS NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.NormalizedName })
            .IsUnique()
            .HasFilter("\"ProductId\" IS NOT NULL AND \"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.ProductId });

        entity.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Inventario_ProductVariationType_Product");
    }
}
