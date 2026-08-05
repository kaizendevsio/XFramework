using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("Product", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_Product");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.StockQuantity).HasPrecision(18, 4).HasDefaultValue(0m);
        entity.Property(e => e.SKU).HasMaxLength(50);
        entity.Property(e => e.Brand).HasMaxLength(100);
        entity.Property(e => e.Weight).HasPrecision(18, 3);
        entity.Property(e => e.Image).HasMaxLength(2048);
        entity.Property(e => e.Rating).HasPrecision(5, 2);
        entity.Property(e => e.Discount).HasPrecision(5, 2);
        entity.Property(e => e.IsAvailable).HasDefaultValue(true);

        entity.Ignore(e => e.Dimensions);
        entity.Ignore(e => e.Tags);
        entity.Ignore(e => e.Reviews);

        entity.HasIndex(e => new { e.TenantId, e.CategoryId });
        entity.HasIndex(e => new { e.TenantId, e.SKU })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"SKU\" IS NOT NULL AND \"SKU\" <> ''");

        entity.HasOne(e => e.Category)
            .WithMany(e => e.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_Product_Category");
    }
}
