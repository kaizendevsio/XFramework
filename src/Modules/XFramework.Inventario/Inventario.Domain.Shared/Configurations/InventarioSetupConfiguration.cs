using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Configurations;

public sealed class InventarioSetupConfiguration : IEntityTypeConfiguration<InventarioSetup>
{
    public void Configure(EntityTypeBuilder<InventarioSetup> entity)
    {
        entity.ToTable("InventarioSetup", "Inventario");
        entity.ConfigureBaseModel("PK_Inventario_Setup");
        entity.Property(x => x.DefaultCurrency).HasMaxLength(3).IsRequired();
        entity.Property(x => x.CompletionHash).HasMaxLength(64);
        entity.HasIndex(x => x.TenantId).IsUnique();
    }
}
