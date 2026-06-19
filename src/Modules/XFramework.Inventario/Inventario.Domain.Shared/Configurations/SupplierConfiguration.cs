using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.ToTable("Supplier", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_Supplier");

        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ContactName).HasMaxLength(200);
        entity.Property(e => e.Email).HasMaxLength(320);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        entity.HasIndex(e => new { e.TenantId, e.Name });
    }
}
