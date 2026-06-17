using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> entity)
    {
        entity.ToTable("ProductCategory", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ProductCategory");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);

        entity.HasIndex(e => new { e.TenantId, e.Name });
    }
}
