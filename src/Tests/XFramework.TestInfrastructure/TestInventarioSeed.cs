using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.TestInfrastructure;

public static class TestInventarioSeed
{
    public static async Task<ProductCategory> SeedCategory(
        AppDbContext db,
        Guid? tenantId = null,
        string? name = null)
    {
        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TestConstants.TenantId,
            Name = name ?? $"Category {Guid.NewGuid():N}",
            Description = "Integration test category",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<ProductCategory>().Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public static async Task<Product> SeedProduct(
        AppDbContext db,
        Guid? tenantId = null,
        Guid? categoryId = null,
        string? sku = null,
        string? name = null)
    {
        var resolvedTenantId = tenantId ?? TestConstants.TenantId;
        var resolvedCategoryId = categoryId ?? (await SeedCategory(db, resolvedTenantId)).Id;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = resolvedTenantId,
            CategoryId = resolvedCategoryId,
            Name = name ?? $"Product {Guid.NewGuid():N}",
            SKU = sku ?? $"SKU-{Guid.NewGuid():N}"[..16],
            Price = 10m,
            StockQuantity = 0,
            IsAvailable = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public static async Task<Warehouse> SeedWarehouse(
        AppDbContext db,
        Guid? tenantId = null,
        string? code = null)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TestConstants.TenantId,
            Code = code ?? $"WH-{Guid.NewGuid():N}"[..12],
            Name = "Integration Warehouse",
            IsDefault = false,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<Warehouse>().Add(warehouse);
        await db.SaveChangesAsync();
        return warehouse;
    }

    public static async Task<InventoryLocation> SeedLocation(
        AppDbContext db,
        Guid warehouseId,
        Guid? tenantId = null,
        string? code = null)
    {
        var location = new InventoryLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TestConstants.TenantId,
            WarehouseId = warehouseId,
            Code = code ?? $"BIN-{Guid.NewGuid():N}"[..13],
            Name = "Integration Bin",
            LocationType = InventoryLocationType.Bin,
            IsPickable = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<InventoryLocation>().Add(location);
        await db.SaveChangesAsync();
        return location;
    }

    public static async Task<InventoryLot> SeedLot(
        AppDbContext db,
        Guid productId,
        Guid? tenantId = null,
        string? lotNumber = null,
        DateTime? expiresAt = null)
    {
        var lot = new InventoryLot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TestConstants.TenantId,
            ProductId = productId,
            LotNumber = lotNumber ?? $"LOT-{Guid.NewGuid():N}"[..16],
            SupplierReference = "integration",
            ReceivedAt = DateTime.UtcNow,
            ManufacturedAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = expiresAt,
            UnitCost = 5m,
            Status = InventoryLotStatus.Available,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<InventoryLot>().Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    public static async Task<Supplier> SeedSupplier(
        AppDbContext db,
        Guid? tenantId = null,
        string? code = null)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TestConstants.TenantId,
            Code = code ?? $"SUP-{Guid.NewGuid():N}"[..13],
            Name = "Integration Supplier",
            ContactName = "Inventory Buyer",
            Email = $"supplier-{Guid.NewGuid():N}@test.local",
            IsActive = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<Supplier>().Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    public static async Task SetInventarioFeature(
        AppDbContext db,
        string? subFeatureKey,
        bool isEnabled,
        Guid? tenantId = null)
    {
        var resolvedTenantId = tenantId ?? TestConstants.TenantId;
        var (moduleKey, normalizedSubFeatureKey) =
            IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.Normalize(
                IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.Inventario,
                subFeatureKey);
        var feature = await db.Set<IdentityServer.Domain.Shared.Contracts.TenantModuleFeature>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.TenantId == resolvedTenantId &&
                x.ModuleKey == moduleKey &&
                x.SubFeatureKey == normalizedSubFeatureKey);

        if (feature is null)
        {
            feature = new IdentityServer.Domain.Shared.Contracts.TenantModuleFeature
            {
                Id = Guid.NewGuid(),
                TenantId = resolvedTenantId,
                ModuleKey = moduleKey,
                SubFeatureKey = normalizedSubFeatureKey,
                DisplayName = string.IsNullOrWhiteSpace(normalizedSubFeatureKey) ? "Inventario" : normalizedSubFeatureKey,
                IsEnabled = isEnabled,
                CreatedAt = DateTime.UtcNow
            };
            db.Set<IdentityServer.Domain.Shared.Contracts.TenantModuleFeature>().Add(feature);
        }
        else
        {
            feature.IsEnabled = isEnabled;
            feature.ModifiedAt = DateTime.UtcNow;
            db.Set<IdentityServer.Domain.Shared.Contracts.TenantModuleFeature>().Update(feature);
        }

        await db.SaveChangesAsync();
    }
}
