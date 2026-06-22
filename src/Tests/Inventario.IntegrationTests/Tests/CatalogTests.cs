using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Catalog)]
[Category(TestCategories.DataContext)]
public sealed class CatalogTests : InventarioTestBase
{
    [Test]
    public async Task SaveChangesAsync_RemoteProductCategoryCreate_PersistsThroughInventarioDataContext()
    {
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            Name = $"Category {Guid.NewGuid():N}",
            Description = "Remote DataContext category",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(category);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var db = CreateDbContext();
        var persisted = await db.Set<ProductCategory>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == category.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(InventarioIntegrationTestFixture.TestTenantId);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductCategoryUpdate_PersistsThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        category.Description = $"Updated {Guid.NewGuid():N}";
        ctx.Update(category);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<ProductCategory>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == category.Id);
        persisted.Description.Should().Be(category.Description);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductCategoryRemove_SoftDeletesThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        ctx.Remove(category);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<ProductCategory>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == category.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_RemoteProductCreate_PersistsWithCategory()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            CategoryId = category.Id,
            Name = $"Product {Guid.NewGuid():N}",
            SKU = $"SKU-{Guid.NewGuid():N}"[..18],
            Price = 25m,
            IsAvailable = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(product);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        var persisted = await db.Set<Product>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == product.Id);
        persisted.Should().NotBeNull();
        persisted!.CategoryId.Should().Be(category.Id);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductUpdate_PersistsThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        product.Name = $"Updated Product {Guid.NewGuid():N}";
        product.Price = 42m;
        ctx.Update(product);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<Product>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == product.Id);
        persisted.Name.Should().Be(product.Name);
        persisted.Price.Should().Be(42m);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductRemove_SoftDeletesThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        ctx.Remove(product);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<Product>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == product.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAt.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductVariationCreate_PersistsWithProduct()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var variation = new ProductVariation
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            VariationType = "Size",
            Name = $"Variation {Guid.NewGuid():N}",
            AdditionalPrice = 2.5m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(variation);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        var persisted = await db.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == variation.Id);
        persisted.Should().NotBeNull();
        persisted!.ProductId.Should().Be(product.Id);
        persisted.VariationType.Should().Be("Size");
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductVariationUpdate_PersistsThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var variation = new ProductVariation
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            VariationType = "Size",
            Name = $"Variation {Guid.NewGuid():N}",
            AdditionalPrice = 2m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<ProductVariation>().Add(variation);
        await db.SaveChangesAsync();
        var ctx = InventarioIntegrationTestFixture.DataContext;

        variation.Name = $"Updated Variation {Guid.NewGuid():N}";
        variation.VariationType = "Color";
        variation.AdditionalPrice = 4m;
        ctx.Update(variation);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == variation.Id);
        persisted.Name.Should().Be(variation.Name);
        persisted.VariationType.Should().Be("Color");
        persisted.AdditionalPrice.Should().Be(4m);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductVariationRemove_SoftDeletesThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var variation = new ProductVariation
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            Name = $"Variation {Guid.NewGuid():N}",
            AdditionalPrice = 2m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<ProductVariation>().Add(variation);
        await db.SaveChangesAsync();
        var ctx = InventarioIntegrationTestFixture.DataContext;

        ctx.Remove(variation);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == variation.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAt.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.Planning)]
    [Category(TestCategories.ControlPanelContract)]
    public async Task SaveChangesAsync_RemoteInventoryReorderRuleCreate_ReturnsNotRegisteredForRemoteMutation()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var rule = new InventoryReorderRule
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            MinimumQuantity = 0,
            MaximumQuantity = 100,
            ReorderPoint = 10,
            ReorderQuantity = 25,
            IsActive = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(rule);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        save.Message.Should().Contain("InventoryReorderRule");
        save.Message.Should().Contain("not registered for remote mutation");
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.ControlPanelContract)]
    public async Task SaveChangesAsync_RemoteAdvancedInventarioEntityCreate_ReturnsNotRegisteredForRemoteMutation()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var lot = await TestInventarioSeed.SeedLot(db, product.Id);
        var stockBalanceId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderLineId = Guid.NewGuid();
        var receivingDocumentId = Guid.NewGuid();

        var rejectedEntities = new (string EntityName, object Entity)[]
        {
            ("Warehouse", WithBase(new Warehouse { Code = UniqueCode("WH"), Name = "Rejected Warehouse" })),
            ("InventoryLocation", WithBase(new InventoryLocation { WarehouseId = warehouse.Id, Code = UniqueCode("BIN"), Name = "Rejected Location" })),
            ("InventoryLot", WithBase(new InventoryLot { ProductId = product.Id, LotNumber = UniqueCode("LOT"), ReceivedAt = DateTime.UtcNow, Status = XFramework.Inventario.Domain.Shared.Enums.InventoryLotStatus.Available })),
            ("InventoryReorderRule", WithBase(new InventoryReorderRule { ProductId = product.Id, MinimumQuantity = 0, MaximumQuantity = 100, ReorderPoint = 10, ReorderQuantity = 20, IsActive = true })),
            ("StockBalance", WithBase(new StockBalance { Id = stockBalanceId, ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, LotId = lot.Id, OnHandQuantity = 10, AvailableQuantity = 10 })),
            ("InventoryMovement", WithBase(new InventoryMovement { ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, LotId = lot.Id, StockBalanceId = stockBalanceId, MovementType = XFramework.Inventario.Domain.Shared.Enums.InventoryMovementType.Adjustment, QuantityDelta = 1, MovementDate = DateTime.UtcNow })),
            ("Reservation", WithBase(new Reservation { Id = reservationId, ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, StockBalanceId = stockBalanceId, Quantity = 1, Status = XFramework.Inventario.Domain.Shared.Enums.ReservationStatus.Active, ReservedAt = DateTime.UtcNow })),
            ("ReservationAllocation", WithBase(new ReservationAllocation { ReservationId = reservationId, ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, StockBalanceId = stockBalanceId, LotId = lot.Id, Quantity = 1, Status = XFramework.Inventario.Domain.Shared.Enums.ReservationAllocationStatus.Reserved, ReservedAt = DateTime.UtcNow })),
            ("Supplier", WithBase(new Supplier { Id = supplierId, Code = UniqueCode("SUP"), Name = "Rejected Supplier", IsActive = true })),
            ("PurchaseOrder", WithBase(new PurchaseOrder { Id = purchaseOrderId, OrderNumber = UniqueCode("PO"), SupplierId = supplierId, Status = XFramework.Inventario.Domain.Shared.Enums.PurchaseOrderStatus.Open, OrderDate = DateTime.UtcNow })),
            ("PurchaseOrderLine", WithBase(new PurchaseOrderLine { Id = purchaseOrderLineId, PurchaseOrderId = purchaseOrderId, ProductId = product.Id, OrderedQuantity = 1, UnitOfMeasure = "ea" })),
            ("ReceivingDocument", WithBase(new ReceivingDocument { Id = receivingDocumentId, ReceiptNumber = UniqueCode("RCV"), PurchaseOrderId = purchaseOrderId, WarehouseId = warehouse.Id, LocationId = location.Id, SupplierId = supplierId, Status = XFramework.Inventario.Domain.Shared.Enums.ReceivingDocumentStatus.Posted, ReceivedAt = DateTime.UtcNow })),
            ("ReceivingLine", WithBase(new ReceivingLine { ReceivingDocumentId = receivingDocumentId, PurchaseOrderLineId = purchaseOrderLineId, ProductId = product.Id, LotId = lot.Id, StockBalanceId = stockBalanceId, Quantity = 1, UnitOfMeasure = "ea" })),
            ("ProductTransaction", WithBase(new ProductTransaction { ProductId = product.Id, Quantity = 1, TotalPrice = 10m, TransactionDate = DateTime.UtcNow }))
        };

        foreach (var (entityName, entity) in rejectedEntities)
        {
            var ctx = InventarioIntegrationTestFixture.DataContext;
            AddUntyped(ctx, entity);

            var save = await ctx.SaveChangesAsync();

            save.IsSuccess.Should().BeFalse(entityName);
            save.Message.Should().Contain(entityName);
            save.Message.Should().Contain("not registered for remote mutation");
        }
    }

    private static T WithBase<T>(T entity) where T : XFramework.Domain.Shared.Contracts.Base.BaseModel
    {
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.TenantId = InventarioIntegrationTestFixture.TestTenantId;
        entity.IsEnabled = true;
        entity.CreatedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        return entity;
    }

    private static void AddUntyped(XFramework.Domain.Shared.DataContext.IDataContext ctx, object entity)
    {
        var method = typeof(XFramework.Domain.Shared.DataContext.IDataContext)
            .GetMethod(nameof(XFramework.Domain.Shared.DataContext.IDataContext.Add))!
            .MakeGenericMethod(entity.GetType());
        method.Invoke(ctx, [entity]);
    }
}
