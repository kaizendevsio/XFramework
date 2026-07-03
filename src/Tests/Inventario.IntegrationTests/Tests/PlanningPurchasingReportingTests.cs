using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
public sealed class PlanningPurchasingReportingTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.Planning)]
    [Category(TestCategories.Wrappers)]
    [Category(TestCategories.PortalContract)]
    public async Task CreateInventoryReorderRule_ValidRequest_PersistsThroughWrapper()
    {
        var seed = await SeedInventoryScope();

        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryReorderRule(
            new CreateInventoryReorderRuleRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                MinimumQuantity = 2,
                MaximumQuantity = 20,
                ReorderPoint = 5,
                ReorderQuantity = 10,
                PreferredSupplier = "portal-wrapper",
                IsActive = true
            });

        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        var persisted = await db.Set<InventoryReorderRule>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.ProductId == seed.Product.Id &&
                x.WarehouseId == seed.Warehouse.Id &&
                x.LocationId == seed.Location.Id &&
                x.PreferredSupplier == "portal-wrapper");

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(InventarioIntegrationTestFixture.TestTenantId);
        persisted.ReorderQuantity.Should().Be(10);
    }

    [Test]
    [Category(TestCategories.Planning)]
    [Category(TestCategories.Reporting)]
    public async Task GetReorderSuggestions_LowStockProduct_ReturnsSuggestedQuantity()
    {
        var seed = await SeedInventoryScope();
        var rule = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryReorderRule(
            new CreateInventoryReorderRuleRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                MinimumQuantity = 5,
                MaximumQuantity = 50,
                ReorderPoint = 10,
                ReorderQuantity = 25,
                PreferredSupplier = "preferred-supplier",
                IsActive = true
            });
        rule.IsSuccess.Should().BeTrue(rule.Message);

        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, quantity: 4);

        var suggestions = await InventarioIntegrationTestFixture.ServiceWrapper.GetReorderSuggestions(
            new GetReorderSuggestionsRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id
            });

        suggestions.IsSuccess.Should().BeTrue(suggestions.Message);
        suggestions.Response.Should().ContainSingle(x =>
            x.ProductId == seed.Product.Id &&
            x.SuggestedQuantity == 46);

        var lowStock = await InventarioIntegrationTestFixture.ServiceWrapper.GetLowStockReport(
            new GetLowStockReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id
            });

        lowStock.IsSuccess.Should().BeTrue(lowStock.Message);
        lowStock.Response.Should().ContainSingle(x =>
            x.ProductId == seed.Product.Id &&
            x.AvailableQuantity == 4 &&
            x.ReorderPoint == 10);
    }

    [Test]
    [Category(TestCategories.Reporting)]
    public async Task GetStockPositionReport_PostedStock_ReturnsProductLocationLotRow()
    {
        var seed = await SeedInventoryScope(withLot: true);
        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, seed.Lot?.Id, 12);

        var report = await InventarioIntegrationTestFixture.ServiceWrapper.GetStockPositionReport(
            new GetStockPositionReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot?.Id
            });

        report.IsSuccess.Should().BeTrue(report.Message);
        report.Response.Should().ContainSingle(x =>
            x.ProductId == seed.Product.Id &&
            x.LotId == seed.Lot!.Id &&
            x.OnHandQuantity == 12);
    }

    [Test]
    [Category(TestCategories.Reporting)]
    public async Task ExpiryAndMovementReports_PostedLots_ReturnFilteredRows()
    {
        var nearExpirySeed = await SeedInventoryScope(withLot: true, expiresAt: DateTime.UtcNow.AddDays(7));
        var expiredSeed = await SeedInventoryScope(withLot: true, expiresAt: DateTime.UtcNow.AddDays(-2));

        await PostOpeningBalance(
            nearExpirySeed.Product.Id,
            nearExpirySeed.Warehouse.Id,
            nearExpirySeed.Location.Id,
            nearExpirySeed.Lot!.Id,
            8,
            referenceType: "near-expiry-report");
        await PostOpeningBalance(
            expiredSeed.Product.Id,
            expiredSeed.Warehouse.Id,
            expiredSeed.Location.Id,
            expiredSeed.Lot!.Id,
            6,
            referenceType: "expired-report");

        var nearExpiry = await InventarioIntegrationTestFixture.ServiceWrapper.GetNearExpiryStockReport(
            new GetNearExpiryStockReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = nearExpirySeed.Product.Id,
                DaysAhead = 30
            });
        var expired = await InventarioIntegrationTestFixture.ServiceWrapper.GetExpiredStockReport(
            new GetExpiredStockReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = expiredSeed.Product.Id
            });
        var movementLedger = await InventarioIntegrationTestFixture.ServiceWrapper.GetMovementLedgerReport(
            new GetMovementLedgerReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = nearExpirySeed.Product.Id,
                LotId = nearExpirySeed.Lot.Id,
                ReferenceType = "near-expiry-report"
            });

        nearExpiry.IsSuccess.Should().BeTrue(nearExpiry.Message);
        nearExpiry.Response.Should().ContainSingle(x => x.LotId == nearExpirySeed.Lot.Id);

        expired.IsSuccess.Should().BeTrue(expired.Message);
        expired.Response.Should().ContainSingle(x => x.LotId == expiredSeed.Lot.Id);

        movementLedger.IsSuccess.Should().BeTrue(movementLedger.Message);
        movementLedger.Response.Should().ContainSingle(x =>
            x.LotId == nearExpirySeed.Lot.Id &&
            x.ReferenceType == "near-expiry-report");
    }

    [Test]
    [Category(TestCategories.Reporting)]
    [Category(TestCategories.Reservations)]
    public async Task GetReservationAllocationStatusReport_ReservedStock_ReturnsAllocationRow()
    {
        var seed = await SeedInventoryScope(withLot: true);
        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, seed.Lot!.Id, 9);
        var referenceId = Guid.NewGuid();

        var reservation = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id,
                Quantity = 3,
                ReferenceType = "allocation-report",
                ReferenceId = referenceId
            });
        reservation.IsSuccess.Should().BeTrue(reservation.Message);

        var report = await InventarioIntegrationTestFixture.ServiceWrapper.GetReservationAllocationStatusReport(
            new GetReservationAllocationStatusReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                LotId = seed.Lot.Id,
                Status = ReservationAllocationStatus.Reserved
            });

        report.IsSuccess.Should().BeTrue(report.Message);
        report.Response.Should().ContainSingle(x =>
            x.ProductId == seed.Product.Id &&
            x.LotId == seed.Lot.Id &&
            x.Quantity == 3 &&
            x.Status == ReservationAllocationStatus.Reserved);
    }

    [Test]
    [Category(TestCategories.Purchasing)]
    public async Task ReceiveInventory_AgainstPurchaseOrder_PostsReceiptAndUpdatesOrderStatus()
    {
        var seed = await SeedInventoryScope();
        var supplierCode = UniqueCode("SUP");
        var supplier = await InventarioIntegrationTestFixture.ServiceWrapper.CreateSupplier(
            new CreateSupplierRequest
            {
                Metadata = CreateMetadata(),
                Code = supplierCode,
                Name = "Integration Supplier",
                IsActive = true
            });
        supplier.IsSuccess.Should().BeTrue(supplier.Message);

        await using var setupDb = CreateDbContext();
        var persistedSupplier = await setupDb.Set<Supplier>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Code == supplierCode);

        var orderNumber = UniqueCode("PO");
        var order = await InventarioIntegrationTestFixture.ServiceWrapper.CreatePurchaseOrder(
            new CreatePurchaseOrderRequest
            {
                Metadata = CreateMetadata(),
                OrderNumber = orderNumber,
                SupplierId = persistedSupplier.Id,
                Status = PurchaseOrderStatus.Open,
                Lines =
                [
                    new PurchaseOrderLineRequest
                    {
                        ProductId = seed.Product.Id,
                        OrderedQuantity = 10,
                        UnitCost = 7m,
                        UnitOfMeasure = "ea"
                    }
                ]
            });
        order.IsSuccess.Should().BeTrue(order.Message);

        var persistedOrder = await setupDb.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Include(x => x.Lines)
            .FirstAsync(x => x.OrderNumber == orderNumber);
        persistedOrder.Lines.Should().ContainSingle();
        var line = persistedOrder.Lines[0];

        var receiptNumber = UniqueCode("RCV");
        var receipt = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(
            new ReceiveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ReceiptNumber = receiptNumber,
                PurchaseOrderId = persistedOrder.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                SupplierId = persistedSupplier.Id,
                ReceivedAt = DateTime.UtcNow,
                IdempotencyKey = $"receive-{Guid.NewGuid():N}",
                Lines =
                [
                    new ReceivingLineRequest
                    {
                        PurchaseOrderLineId = line.Id,
                        ProductId = seed.Product.Id,
                        Quantity = 5,
                        UnitCost = 7m,
                        UnitOfMeasure = "ea",
                        LotNumber = UniqueCode("LOT"),
                        ExpiresAt = DateTime.UtcNow.AddDays(180)
                    }
                ]
            });

        receipt.IsSuccess.Should().BeTrue(receipt.Message);

        await using var db = CreateDbContext();
        var persistedReceipt = await db.Set<ReceivingDocument>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReceiptNumber == receiptNumber);
        persistedReceipt.Status.Should().Be(ReceivingDocumentStatus.Posted);

        var updatedOrder = await db.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == persistedOrder.Id);
        updatedOrder.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        var movement = await db.Set<InventoryMovement>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ReferenceId == persistedReceipt.Id && x.MovementType == InventoryMovementType.Receipt);
        movement.Should().NotBeNull();
    }

    [Test]
    [Category(TestCategories.Purchasing)]
    public async Task ReceiveInventory_SameIdempotencyKeyAndPayload_ReplaysWithoutDuplicateDocument()
    {
        var seed = await SeedInventoryScope();
        var key = $"receive-idem-{Guid.NewGuid():N}";
        var receivedAt = DateTime.UtcNow;
        var receiptNumber = UniqueCode("RCV");
        var request = new ReceiveInventoryRequest
        {
            Metadata = CreateMetadata(),
            ReceiptNumber = receiptNumber,
            WarehouseId = seed.Warehouse.Id,
            LocationId = seed.Location.Id,
            ReceivedAt = receivedAt,
            IdempotencyKey = key,
            Lines =
            [
                new ReceivingLineRequest
                {
                    ProductId = seed.Product.Id,
                    Quantity = 3,
                    UnitCost = 5m,
                    UnitOfMeasure = "ea"
                }
            ]
        };

        var first = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(request);
        var replay = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        replay.IsSuccess.Should().BeTrue(replay.Message);

        await using var db = CreateDbContext();
        var documentCount = await db.Set<ReceivingDocument>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.IdempotencyKey == key);
        documentCount.Should().Be(1);
    }

    private async Task<InventoryScope> SeedInventoryScope(
        bool withLot = false,
        DateTime? expiresAt = null)
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var lot = withLot
            ? await TestInventarioSeed.SeedLot(db, product.Id, expiresAt: expiresAt ?? DateTime.UtcNow.AddDays(120))
            : null;
        return new InventoryScope(product, warehouse, location, lot);
    }

    private static async Task PostOpeningBalance(
        Guid productId,
        Guid warehouseId,
        Guid locationId,
        Guid? lotId = null,
        decimal quantity = 1,
        string? referenceType = null)
    {
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(
            new PostStockMovementRequest
            {
                Metadata = CreateMetadata(),
                ProductId = productId,
                WarehouseId = warehouseId,
                LocationId = locationId,
                LotId = lotId,
                MovementType = InventoryMovementType.OpeningBalance,
                Quantity = quantity,
                ReferenceType = referenceType,
                IdempotencyKey = $"planning-stock-{Guid.NewGuid():N}"
            });
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private sealed record InventoryScope(
        Product Product,
        Warehouse Warehouse,
        InventoryLocation Location,
        InventoryLot? Lot);
}
