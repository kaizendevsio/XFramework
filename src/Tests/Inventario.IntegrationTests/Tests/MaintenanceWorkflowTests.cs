using System.Net;
using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Wrappers)]
public sealed class MaintenanceWorkflowTests : InventarioTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ReceiveInventory_MismatchedOrderIdentity_RejectsWithoutPostingThenReceivesExactlyOnce(bool mismatchVariant)
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var otherProduct = await TestInventarioSeed.SeedProduct(db);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var variant = new ProductVariation { Id = Guid.NewGuid(), TenantId = product.TenantId,
            ProductId = product.Id, Name = "Medium", VariationType = "Size", Price = 10,
            IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() };
        db.Add(variant);
        var order = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = product.TenantId,
            OrderNumber = UniqueCode("PO-IDENTITY"), Status = PurchaseOrderStatus.Open,
            OrderDate = DateTime.UtcNow, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() };
        var line = new PurchaseOrderLine { Id = Guid.NewGuid(), TenantId = product.TenantId,
            PurchaseOrderId = order.Id, ProductId = product.Id, ProductVariationId = variant.Id,
            OrderedQuantity = 5, UnitCost = 3, UnitOfMeasure = "each", IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() };
        db.AddRange(order, line);
        await db.SaveChangesAsync();
        var receipt = new ReceiveInventoryRequest { Metadata = CreateMetadata(), PurchaseOrderId = order.Id,
            WarehouseId = warehouse.Id, LocationId = location.Id, IdempotencyKey = UniqueCode("RCV-ID"),
            Lines = [new ReceivingLineRequest { PurchaseOrderLineId = line.Id,
                ProductId = mismatchVariant ? product.Id : otherProduct.Id,
                ProductVariationId = mismatchVariant ? null : variant.Id, Quantity = 2, UnitCost = 3, UnitOfMeasure = "each" }] };
        var rejected = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(receipt);
        rejected.IsSuccess.Should().BeFalse();
        rejected.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await db.Set<ReceivingDocument>().CountAsync(x => x.PurchaseOrderId == order.Id)).Should().Be(0);
        (await db.Set<StockBalance>().CountAsync(x => x.ProductId == product.Id)).Should().Be(0);

        var corrected = receipt with { Lines = [receipt.Lines[0] with { ProductId = product.Id, ProductVariationId = variant.Id }] };
        var posted = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(corrected);
        posted.IsSuccess.Should().BeTrue(posted.Message);
        var replay = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(corrected);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        (await db.Set<ReceivingDocument>().CountAsync(x => x.PurchaseOrderId == order.Id)).Should().Be(1);
        (await db.Set<PurchaseOrderLine>().AsNoTracking().SingleAsync(x => x.Id == line.Id)).ReceivedQuantity.Should().Be(2);
        var balance = await db.Set<StockBalance>().AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        balance.ProductVariationId.Should().Be(variant.Id);
        balance.OnHandQuantity.Should().Be(2);
    }

    [Test]
    public async Task UpdateWarehouse_Wrapper_PersistsAndRejectsInvalidStaleAndOtherTenantRecords()
    {
        await using var db = CreateDbContext();
        var record = await TestInventarioSeed.SeedWarehouse(db);
        var request = new UpdateWarehouseRequest
        {
            Metadata = CreateMetadata(), Id = record.Id, ConcurrencyStamp = record.ConcurrencyStamp,
            Name = "Edited warehouse", CountryCode = "SG"
        };
        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateWarehouse(request with { CountryCode = "Singapore" });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateWarehouse(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        var persisted = await db.Set<Warehouse>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        persisted.Name.Should().Be("Edited warehouse");
        persisted.Code.Should().Be(record.Code);
        persisted.ConcurrencyStamp.Should().NotBe(record.ConcurrencyStamp);

        var stale = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateWarehouse(request);
        stale.IsSuccess.Should().BeFalse();
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var otherTenant = Guid.NewGuid();
        var foreign = await TestInventarioSeed.SeedWarehouse(db, otherTenant);
        var denied = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateWarehouse(request with { Id = foreign.Id, ConcurrencyStamp = foreign.ConcurrencyStamp });
        denied.IsSuccess.Should().BeFalse();
        denied.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        var unchanged = await db.Set<Warehouse>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == foreign.Id);
        unchanged.ConcurrencyStamp.Should().Be(foreign.ConcurrencyStamp);
    }

    [Test]
    public async Task UpdateInventoryLocation_Wrapper_PersistsAndRejectsInvalidStaleAndOtherTenantRecords()
    {
        await using var db = CreateDbContext();
        var record = await TestInventarioSeed.SeedLocation(db, (await TestInventarioSeed.SeedWarehouse(db)).Id);
        var request = new UpdateInventoryLocationRequest
        {
            Metadata = CreateMetadata(), Id = record.Id, ConcurrencyStamp = record.ConcurrencyStamp,
            Name = "Edited bin", LocationType = InventoryLocationType.Bin, IsPickable = false
        };
        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLocation(request with { Description = new string('x', 1001) });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLocation(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        var persisted = await db.Set<InventoryLocation>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        persisted.IsPickable.Should().Be(false);
        persisted.WarehouseId.Should().Be(record.WarehouseId);
        persisted.ConcurrencyStamp.Should().NotBe(record.ConcurrencyStamp);

        var stale = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLocation(request);
        stale.IsSuccess.Should().BeFalse();
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var otherTenant = Guid.NewGuid();
        var foreign = await TestInventarioSeed.SeedLocation(db, (await TestInventarioSeed.SeedWarehouse(db, otherTenant)).Id, otherTenant);
        var denied = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLocation(request with { Id = foreign.Id, ConcurrencyStamp = foreign.ConcurrencyStamp });
        denied.IsSuccess.Should().BeFalse();
        denied.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        var unchanged = await db.Set<InventoryLocation>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == foreign.Id);
        unchanged.ConcurrencyStamp.Should().Be(foreign.ConcurrencyStamp);
    }

    [Test]
    public async Task UpdateSupplier_Wrapper_PersistsAndRejectsInvalidStaleAndOtherTenantRecords()
    {
        await using var db = CreateDbContext();
        var record = await TestInventarioSeed.SeedSupplier(db);
        var request = new UpdateSupplierRequest
        {
            Metadata = CreateMetadata(), Id = record.Id, ConcurrencyStamp = record.ConcurrencyStamp,
            Name = "Edited supplier", IsActive = false
        };
        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateSupplier(request with { Phone = new string('x', 51) });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateSupplier(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        var persisted = await db.Set<Supplier>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        persisted.IsActive.Should().Be(false);
        persisted.Code.Should().Be(record.Code);
        persisted.ConcurrencyStamp.Should().NotBe(record.ConcurrencyStamp);

        var stale = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateSupplier(request);
        stale.IsSuccess.Should().BeFalse();
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var otherTenant = Guid.NewGuid();
        var foreign = await TestInventarioSeed.SeedSupplier(db, otherTenant);
        var denied = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateSupplier(request with { Id = foreign.Id, ConcurrencyStamp = foreign.ConcurrencyStamp });
        denied.IsSuccess.Should().BeFalse();
        denied.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        var unchanged = await db.Set<Supplier>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == foreign.Id);
        unchanged.ConcurrencyStamp.Should().Be(foreign.ConcurrencyStamp);
    }

    [Test]
    public async Task UpdateInventoryLot_Wrapper_PersistsAndRejectsInvalidStaleAndOtherTenantRecords()
    {
        await using var db = CreateDbContext();
        var record = await TestInventarioSeed.SeedLot(db, (await TestInventarioSeed.SeedProduct(db)).Id);
        var request = new UpdateInventoryLotRequest
        {
            Metadata = CreateMetadata(), Id = record.Id, ConcurrencyStamp = record.ConcurrencyStamp,
            SupplierReference = "Updated batch", Status = InventoryLotStatus.Quarantined
        };
        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLot(request with { SupplierReference = new string('x', 201) });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLot(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        var persisted = await db.Set<InventoryLot>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        persisted.Status.Should().Be(InventoryLotStatus.Quarantined);
        persisted.ProductId.Should().Be(record.ProductId);
        persisted.ConcurrencyStamp.Should().NotBe(record.ConcurrencyStamp);

        var stale = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLot(request);
        stale.IsSuccess.Should().BeFalse();
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var otherTenant = Guid.NewGuid();
        var foreign = await TestInventarioSeed.SeedLot(db, (await TestInventarioSeed.SeedProduct(db, otherTenant)).Id, otherTenant);
        var denied = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLot(request with { Id = foreign.Id, ConcurrencyStamp = foreign.ConcurrencyStamp });
        denied.IsSuccess.Should().BeFalse();
        denied.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        var unchanged = await db.Set<InventoryLot>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == foreign.Id);
        unchanged.ConcurrencyStamp.Should().Be(foreign.ConcurrencyStamp);
    }

    [Test]
    public async Task UpdateInventoryReorderRule_Wrapper_PersistsAndRejectsInvalidStaleAndOtherTenantRecords()
    {
        await using var db = CreateDbContext();
        var record = await SeedRule(db, InventarioIntegrationTestFixture.TestTenantId);
        var request = new UpdateInventoryReorderRuleRequest
        {
            Metadata = CreateMetadata(), Id = record.Id, ConcurrencyStamp = record.ConcurrencyStamp,
            MinimumQuantity = 2, MaximumQuantity = 30, ReorderPoint = 5, ReorderQuantity = 6, IsActive = false
        };
        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryReorderRule(request with { ReorderQuantity = 0 });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryReorderRule(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        var persisted = await db.Set<InventoryReorderRule>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        persisted.IsActive.Should().Be(false);
        persisted.ProductId.Should().Be(record.ProductId);
        persisted.ConcurrencyStamp.Should().NotBe(record.ConcurrencyStamp);

        var stale = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryReorderRule(request);
        stale.IsSuccess.Should().BeFalse();
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var otherTenant = Guid.NewGuid();
        var foreign = await SeedRule(db, otherTenant);
        var denied = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryReorderRule(request with { Id = foreign.Id, ConcurrencyStamp = foreign.ConcurrencyStamp });
        denied.IsSuccess.Should().BeFalse();
        denied.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        var unchanged = await db.Set<InventoryReorderRule>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == foreign.Id);
        unchanged.ConcurrencyStamp.Should().Be(foreign.ConcurrencyStamp);
    }

    private static async Task<InventoryReorderRule> SeedRule(XFramework.Domain.Contexts.AppDbContext db, Guid tenantId)
    {
        var product = await TestInventarioSeed.SeedProduct(db, tenantId);
        var rule = new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id,
            MinimumQuantity = 1, ReorderPoint = 5, ReorderQuantity = 6, MaximumQuantity = 20,
            IsActive = true, IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() };
        db.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }
}
