using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Wrappers)]
public sealed class CalendarDateWorkflowTests : InventarioTestBase
{
    private static DateTime PickerDate => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(7), DateTimeKind.Unspecified);

    [Test]
    public async Task ReserveInventory_CalendarExpiry_PersistsAsUtc()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var opening = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(new PostStockMovementRequest
        {
            Metadata = CreateMetadata(), ProductId = product.Id, WarehouseId = warehouse.Id,
            LocationId = location.Id, MovementType = InventoryMovementType.Receipt, Quantity = 5
        });
        opening.IsSuccess.Should().BeTrue(opening.Message);
        var expiry = PickerDate;
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(new ReserveInventoryRequest
        {
            Metadata = CreateMetadata(), ProductId = product.Id, WarehouseId = warehouse.Id,
            LocationId = location.Id, Quantity = 2, ExpiresAt = expiry
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        var saved = await db.Set<Reservation>().AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        saved.ExpiresAt.Should().Be(DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
        saved.ExpiresAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        var balance = await db.Set<StockBalance>().AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        balance.OnHandQuantity.Should().Be(5);
        balance.ReservedQuantity.Should().Be(2);
    }

    [Test]
    public async Task CreatePurchaseOrder_CalendarExpectedDate_PersistsAsUtc()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var date = PickerDate;
        var number = UniqueCode("PO-CALENDAR");
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreatePurchaseOrder(new()
        {
            Metadata = CreateMetadata(), OrderNumber = number, ExpectedDate = date,
            Lines = [new() { ProductId = product.Id, OrderedQuantity = 10, UnitOfMeasure = "each" }]
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        var saved = await db.Set<PurchaseOrder>().AsNoTracking().SingleAsync(x => x.OrderNumber == number);
        saved.ExpectedDate.Should().Be(DateTime.SpecifyKind(date, DateTimeKind.Utc));
        saved.ExpectedDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public async Task ReceiveInventory_MixedBaseAndVariantWithCalendarLotExpiry_PostsExactlyOnce()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var variant = new ProductVariation { Id = Guid.NewGuid(), TenantId = product.TenantId,
            ProductId = product.Id, Name = "Calendar variant", VariationType = "Size", Price = 2.5m,
            IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() };
        db.Add(variant);
        await db.SaveChangesAsync();
        var number = UniqueCode("PO-MIXED");
        var created = await InventarioIntegrationTestFixture.ServiceWrapper.CreatePurchaseOrder(new()
        {
            Metadata = CreateMetadata(), OrderNumber = number,
            Lines = [new() { ProductId = product.Id, OrderedQuantity = 10, UnitCost = 2.5m, UnitOfMeasure = "each" },
                new() { ProductId = product.Id, ProductVariationId = variant.Id, OrderedQuantity = 8, UnitCost = 2.5m, UnitOfMeasure = "each" }]
        });
        created.IsSuccess.Should().BeTrue(created.Message);
        var order = await db.Set<PurchaseOrder>().SingleAsync(x => x.OrderNumber == number);
        var lines = await db.Set<PurchaseOrderLine>().AsNoTracking().Where(x => x.PurchaseOrderId == order.Id).ToListAsync();
        var expiry = PickerDate;
        var lotNumber = UniqueCode("LOT-CALENDAR");
        var receipt = new ReceiveInventoryRequest
        {
            Metadata = CreateMetadata(), PurchaseOrderId = order.Id, WarehouseId = warehouse.Id,
            LocationId = location.Id, IdempotencyKey = UniqueCode("RCV-CALENDAR"),
            Lines = [new() { PurchaseOrderLineId = lines.Single(x => x.ProductVariationId == null).Id,
                ProductId = product.Id, Quantity = 4, UnitCost = 2.5m, UnitOfMeasure = "each" },
                new() { PurchaseOrderLineId = lines.Single(x => x.ProductVariationId == variant.Id).Id,
                    ProductId = product.Id, ProductVariationId = variant.Id, Quantity = 3,
                    UnitCost = 2.5m, UnitOfMeasure = "each", LotNumber = lotNumber, ExpiresAt = expiry }]
        };
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(receipt);
        result.IsSuccess.Should().BeTrue(result.Message);
        var replay = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(receipt);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        (await db.Set<ReceivingDocument>().CountAsync(x => x.PurchaseOrderId == order.Id)).Should().Be(1);
        var lot = await db.Set<InventoryLot>().AsNoTracking().SingleAsync(x => x.LotNumber == lotNumber);
        lot.ManufacturedAt.Should().BeNull();
        lot.ExpiresAt.Should().Be(DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
        lot.ExpiresAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        var balances = await db.Set<StockBalance>().AsNoTracking().Where(x => x.ProductId == product.Id).ToListAsync();
        balances.Single(x => x.ProductVariationId == null).OnHandQuantity.Should().Be(4);
        balances.Single(x => x.ProductVariationId == variant.Id && x.LotId == lot.Id).OnHandQuantity.Should().Be(3);
        var updatedLines = await db.Set<PurchaseOrderLine>().AsNoTracking().Where(x => x.PurchaseOrderId == order.Id).ToListAsync();
        updatedLines.Single(x => x.ProductVariationId == null).ReceivedQuantity.Should().Be(4);
        updatedLines.Single(x => x.ProductVariationId == variant.Id).ReceivedQuantity.Should().Be(3);
    }

    [Test]
    public async Task UpdateInventoryLot_CalendarDates_PersistAsUtc()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var lot = await TestInventarioSeed.SeedLot(db, product.Id);
        var expiry = PickerDate;
        var manufactured = expiry.AddDays(-7);
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.UpdateInventoryLot(new()
        {
            Metadata = CreateMetadata(), Id = lot.Id, ConcurrencyStamp = lot.ConcurrencyStamp,
            Status = InventoryLotStatus.Available, ManufacturedAt = manufactured, ExpiresAt = expiry
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        var saved = await db.Set<InventoryLot>().AsNoTracking().SingleAsync(x => x.Id == lot.Id);
        saved.ManufacturedAt.Should().Be(DateTime.SpecifyKind(manufactured, DateTimeKind.Utc));
        saved.ExpiresAt.Should().Be(DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
        saved.ManufacturedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        saved.ExpiresAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
