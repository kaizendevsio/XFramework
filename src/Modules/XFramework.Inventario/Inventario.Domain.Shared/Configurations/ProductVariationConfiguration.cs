using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
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
        entity.Property(e => e.CombinationKey).HasMaxLength(64);
        entity.Property(e => e.CombinationOptions)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<List<ProductVariationOption>>(value, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(new ValueComparer<List<ProductVariationOption>?>(
                (left, right) => left == right || left != null && right != null && left.SequenceEqual(right),
                value => value == null ? 0 : value.Aggregate(0, (hash, option) => HashCode.Combine(hash, option.GetHashCode())),
                value => value == null ? null : value.ToList()));

        entity.HasIndex(e => new { e.TenantId, e.ProductId });
        entity.HasIndex(e => new { e.TenantId, e.ProductVariationTypeId });
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.CombinationKey })
            .IsUnique()
            .HasDatabaseName("UX_ProductVariation_Tenant_Product_Combination")
            .HasFilter("\"IsDeleted\" = false AND \"CombinationKey\" IS NOT NULL");

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
