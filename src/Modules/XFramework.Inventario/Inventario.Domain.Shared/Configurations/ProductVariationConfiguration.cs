using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ProductVariationConfiguration : IEntityTypeConfiguration<ProductVariation>
{
    public void Configure(EntityTypeBuilder<ProductVariation> entity)
    {
        entity.ToTable("ProductVariation", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ProductVariation");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.VariationType).HasMaxLength(100);
        entity.Property(e => e.AdditionalPrice).HasPrecision(18, 2);
        entity.Property(e => e.Price).HasPrecision(18, 2);

        entity.HasIndex(e => new { e.TenantId, e.ProductId });
        entity.HasIndex(e => new { e.TenantId, e.ProductVariationTypeId });

        entity.HasOne(e => e.Product)
            .WithMany(e => e.Variations)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Inventario_ProductVariation_Product");

        entity.HasOne(e => e.ProductVariationType)
            .WithMany()
            .HasForeignKey(e => e.ProductVariationTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ProductVariation_ProductVariationType");
    }
}
