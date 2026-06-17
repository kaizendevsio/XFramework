using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class ProductTransactionConfiguration : IEntityTypeConfiguration<ProductTransaction>
{
    public void Configure(EntityTypeBuilder<ProductTransaction> entity)
    {
        entity.ToTable("ProductTransaction", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_ProductTransaction");

        entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
        entity.Property(e => e.TransactionDate).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.TransactionDate });

        entity.HasOne(e => e.Product)
            .WithMany(e => e.Transactions)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Inventario_ProductTransaction_Product");
    }
}
