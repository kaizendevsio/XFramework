using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;
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
    [Category(TestCategories.Wrappers)]
    public async Task ProductVariationTypes_CreateThroughWrapper_ReturnsTenantAndProductScopedTypesAndRejectsDuplicates()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);

        var tenantTypeName = $"Size {Guid.NewGuid():N}";
        var productTypeName = $"Pack {Guid.NewGuid():N}";

        var tenantWide = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariationType(
            new CreateProductVariationTypeRequest
            {
                Metadata = CreateMetadata(),
                Name = tenantTypeName,
                Code = "SIZE"
            });
        var duplicate = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariationType(
            new CreateProductVariationTypeRequest
            {
                Metadata = CreateMetadata(),
                Name = tenantTypeName
            });
        var productLocal = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariationType(
            new CreateProductVariationTypeRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                Name = productTypeName
            });

        tenantWide.IsSuccess.Should().BeTrue(tenantWide.Message);
        duplicate.IsSuccess.Should().BeFalse();
        duplicate.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        productLocal.IsSuccess.Should().BeTrue(productLocal.Message);

        var types = await InventarioIntegrationTestFixture.ServiceWrapper.GetProductVariationTypes(
            new GetProductVariationTypesRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id
            });

        types.IsSuccess.Should().BeTrue(types.Message);
        types.Response.Should().Contain(x => x.Name == tenantTypeName && x.ProductId == null);
        types.Response.Should().Contain(x => x.Name == productTypeName && x.ProductId == product.Id);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.Wrappers)]
    public async Task ProductVariations_CreateAndUpdateThroughWrapper_PersistAbsolutePriceAndLegacyDelta()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        product.Price = 12m;
        db.Set<Product>().Update(product);
        await db.SaveChangesAsync();

        var typeName = $"Color {Guid.NewGuid():N}";
        var type = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariationType(
            new CreateProductVariationTypeRequest
            {
                Metadata = CreateMetadata(),
                Name = typeName
            });
        type.IsSuccess.Should().BeTrue(type.Message);

        await using var typeDb = CreateDbContext();
        var persistedType = await typeDb.Set<ProductVariationType>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Name == typeName);

        var variantName = $"Blue {Guid.NewGuid():N}";
        var create = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariation(
            new CreateProductVariationRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                ProductVariationTypeId = persistedType.Id,
                Name = variantName,
                Price = 15.5m
            });
        create.IsSuccess.Should().BeTrue(create.Message);

        await using var createdDb = CreateDbContext();
        var variation = await createdDb.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ProductId == product.Id && x.Name == variantName);
        variation.ProductVariationTypeId.Should().Be(persistedType.Id);
        variation.Price.Should().Be(15.5m);
        variation.AdditionalPrice.Should().Be(3.5m);
        variation.VariationType.Should().Be(typeName);

        var updatedName = $"Blue Updated {Guid.NewGuid():N}";
        var update = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateProductVariation(
            new UpdateProductVariationRequest
            {
                Metadata = CreateMetadata(),
                ProductVariationId = variation.Id,
                ProductVariationTypeId = persistedType.Id,
                Name = updatedName,
                Price = 20m
            });
        update.IsSuccess.Should().BeTrue(update.Message);

        await using var verifyDb = CreateDbContext();
        var updated = await verifyDb.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == variation.Id);
        updated.Name.Should().Be(updatedName);
        updated.Price.Should().Be(20m);
        updated.AdditionalPrice.Should().Be(8m);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.PortalContract)]
    public async Task SaveChangesAsync_RemoteProductVariationCreate_ReturnsNotRegisteredForRemoteMutation()
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
            Price = product.Price + 2.5m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(variation);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        save.Message.Should().Contain("ProductVariation");
        save.Message.Should().Contain("not registered for remote mutation");
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.PortalContract)]
    public async Task SaveChangesAsync_RemoteProductVariationTypeCreate_ReturnsNotRegisteredForRemoteMutation()
    {
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var type = new ProductVariationType
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            Name = $"Type {Guid.NewGuid():N}",
            NormalizedName = $"TYPE-{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(type);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        save.Message.Should().Contain("ProductVariationType");
        save.Message.Should().Contain("not registered for remote mutation");
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.Planning)]
    [Category(TestCategories.PortalContract)]
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
    [Category(TestCategories.PortalContract)]
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
        var variationTypeId = Guid.NewGuid();

        var rejectedEntities = new (string EntityName, object Entity)[]
        {
            ("ProductVariationType", WithBase(new ProductVariationType { Id = variationTypeId, Name = "Rejected Type", NormalizedName = UniqueCode("TYPE") })),
            ("ProductVariation", WithBase(new ProductVariation { ProductId = product.Id, ProductVariationTypeId = variationTypeId, VariationType = "Rejected Type", Name = "Rejected Variant", Price = 12m, AdditionalPrice = 2m })),
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
