using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperContractCoverageTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.Warehousing)]
    public async Task GetWarehouseAndLocationWrappers_TenantScopedRecords_ReturnOnlyRequestTenant()
    {
        await using var db = CreateDbContext();
        var warehouse = await TestInventarioSeed.SeedWarehouse(db, code: UniqueCode("WH"));
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id, code: UniqueCode("BIN"));
        var otherTenantId = Guid.NewGuid();
        var otherWarehouse = await TestInventarioSeed.SeedWarehouse(db, otherTenantId, UniqueCode("WH"));
        var otherLocation = await TestInventarioSeed.SeedLocation(db, otherWarehouse.Id, otherTenantId, UniqueCode("BIN"));

        var warehouses = await InventarioIntegrationTestFixture.ServiceWrapper.GetWarehouses(
            new GetWarehousesRequest { Metadata = CreateMetadata() });
        var locations = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryLocations(
            new GetInventoryLocationsRequest { Metadata = CreateMetadata() });

        warehouses.IsSuccess.Should().BeTrue(warehouses.Message);
        warehouses.Response.Should().Contain(x => x.Id == warehouse.Id);
        warehouses.Response.Should().NotContain(x => x.Id == otherWarehouse.Id);

        locations.IsSuccess.Should().BeTrue(locations.Message);
        locations.Response.Should().Contain(x => x.Id == location.Id);
        locations.Response.Should().NotContain(x => x.Id == otherLocation.Id);
    }

    [Test]
    [Category(TestCategories.Traceability)]
    public async Task GetInventoryLotWrappers_ProductScopedRecords_ReturnListAndDetail()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var lot = await TestInventarioSeed.SeedLot(db, product.Id, lotNumber: UniqueCode("LOT"));
        var otherTenantId = Guid.NewGuid();
        var otherCategory = await TestInventarioSeed.SeedCategory(db, otherTenantId);
        var otherProduct = await TestInventarioSeed.SeedProduct(db, otherTenantId, otherCategory.Id);
        var otherLot = await TestInventarioSeed.SeedLot(db, otherProduct.Id, otherTenantId, UniqueCode("LOT"));

        var lots = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryLots(
            new GetInventoryLotsRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                IncludeExpired = true
            });
        var detail = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryLot(
            new GetInventoryLotRequest
            {
                Metadata = CreateMetadata(),
                Id = lot.Id
            });

        lots.IsSuccess.Should().BeTrue(lots.Message);
        lots.Response.Should().ContainSingle(x => x.Id == lot.Id);
        lots.Response.Should().NotContain(x => x.Id == otherLot.Id);

        detail.IsSuccess.Should().BeTrue(detail.Message);
        detail.Response.Should().NotBeNull();
        detail.Response!.Id.Should().Be(lot.Id);
        detail.Response.ProductId.Should().Be(product.Id);
    }

    [Test]
    [Category(TestCategories.Planning)]
    public async Task GetInventoryReorderRules_ProductFilter_ReturnsPersistedRule()
    {
        var seed = await SeedInventoryScope();
        var create = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryReorderRule(
            new CreateInventoryReorderRuleRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                MinimumQuantity = 2,
                MaximumQuantity = 50,
                ReorderPoint = 8,
                ReorderQuantity = 20,
                PreferredSupplier = "wrapper-coverage",
                IsActive = true
            });
        create.IsSuccess.Should().BeTrue(create.Message);

        var rules = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryReorderRules(
            new GetInventoryReorderRulesRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id
            });

        rules.IsSuccess.Should().BeTrue(rules.Message);
        rules.Response.Should().ContainSingle(x =>
            x.ProductId == seed.Product.Id &&
            x.WarehouseId == seed.Warehouse.Id &&
            x.LocationId == seed.Location.Id &&
            x.ReorderQuantity == 20);
    }

    [Test]
    [Category(TestCategories.Purchasing)]
    public async Task PurchasingReadWrappers_TenantScopedRecords_ReturnExpectedRows()
    {
        var seed = await SeedInventoryScope();
        var supplier = await CreateSupplier();
        var order = await CreatePurchaseOrder(seed.Product.Id, supplier.Id, PurchaseOrderStatus.Open);
        var receipt = await ReceiveInventory(seed, order);

        await using var setupDb = CreateDbContext();
        var otherTenantId = Guid.NewGuid();
        var otherWarehouse = WithBase(
            new Warehouse
            {
                Code = UniqueCode("WH"),
                Name = "Other Tenant Warehouse"
            },
            otherTenantId);
        var otherLocation = WithBase(
            new InventoryLocation
            {
                WarehouseId = otherWarehouse.Id,
                Code = UniqueCode("BIN"),
                Name = "Other Tenant Location"
            },
            otherTenantId);
        var otherSupplier = WithBase(
            new Supplier
            {
                Code = UniqueCode("SUP"),
                Name = "Other Tenant Supplier",
                IsActive = true
            },
            otherTenantId);
        var otherOrder = WithBase(
            new PurchaseOrder
            {
                OrderNumber = UniqueCode("PO"),
                SupplierId = otherSupplier.Id,
                Status = PurchaseOrderStatus.Open,
                OrderDate = DateTime.UtcNow
            },
            otherTenantId);
        var otherReceipt = WithBase(
            new ReceivingDocument
            {
                ReceiptNumber = UniqueCode("RCV"),
                PurchaseOrderId = otherOrder.Id,
                WarehouseId = otherWarehouse.Id,
                LocationId = otherLocation.Id,
                SupplierId = otherSupplier.Id,
                Status = ReceivingDocumentStatus.Posted,
                ReceivedAt = DateTime.UtcNow
            },
            otherTenantId);
        setupDb.Set<Warehouse>().Add(otherWarehouse);
        setupDb.Set<InventoryLocation>().Add(otherLocation);
        setupDb.Set<Supplier>().Add(otherSupplier);
        setupDb.Set<PurchaseOrder>().Add(otherOrder);
        setupDb.Set<ReceivingDocument>().Add(otherReceipt);
        await setupDb.SaveChangesAsync();

        var suppliers = await InventarioIntegrationTestFixture.ServiceWrapper.GetSuppliers(
            new GetSuppliersRequest { Metadata = CreateMetadata() });
        var orders = await InventarioIntegrationTestFixture.ServiceWrapper.GetPurchaseOrders(
            new GetPurchaseOrdersRequest
            {
                Metadata = CreateMetadata()
            });
        var orderDetail = await InventarioIntegrationTestFixture.ServiceWrapper.GetPurchaseOrder(
            new GetPurchaseOrderRequest
            {
                Metadata = CreateMetadata(),
                Id = order.Id
            });
        var receivingDocuments = await InventarioIntegrationTestFixture.ServiceWrapper.GetReceivingDocuments(
            new GetReceivingDocumentsRequest
            {
                Metadata = CreateMetadata(),
                Status = ReceivingDocumentStatus.Posted
            });

        suppliers.IsSuccess.Should().BeTrue(suppliers.Message);
        suppliers.Response.Should().Contain(x => x.Id == supplier.Id);
        suppliers.Response.Should().NotContain(x => x.Id == otherSupplier.Id);

        orders.IsSuccess.Should().BeTrue(orders.Message);
        orders.Response.Should().Contain(x => x.Id == order.Id);
        orders.Response.Should().NotContain(x => x.Id == otherOrder.Id);

        orderDetail.IsSuccess.Should().BeTrue(orderDetail.Message);
        orderDetail.Response.Should().NotBeNull();
        orderDetail.Response!.Id.Should().Be(order.Id);
        orderDetail.Response.SupplierId.Should().Be(supplier.Id);

        receivingDocuments.IsSuccess.Should().BeTrue(receivingDocuments.Message);
        receivingDocuments.Response.Should().Contain(x => x.Id == receipt.Id);
        receivingDocuments.Response.Should().NotContain(x => x.Id == otherReceipt.Id);
    }

    [Test]
    [Category(TestCategories.Purchasing)]
    public async Task SetPurchaseOrderStatus_OpenOrder_UpdatesStatusAndRejectsMissingOrder()
    {
        var product = await SeedProduct();
        var supplier = await CreateSupplier();
        var order = await CreatePurchaseOrder(product.Id, supplier.Id, PurchaseOrderStatus.Open);

        var update = await InventarioIntegrationTestFixture.ServiceWrapper.SetPurchaseOrderStatus(
            new SetPurchaseOrderStatusRequest
            {
                Metadata = CreateMetadata(),
                PurchaseOrderId = order.Id,
                Status = PurchaseOrderStatus.Cancelled
            });
        var missing = await InventarioIntegrationTestFixture.ServiceWrapper.SetPurchaseOrderStatus(
            new SetPurchaseOrderStatusRequest
            {
                Metadata = CreateMetadata(),
                PurchaseOrderId = Guid.NewGuid(),
                Status = PurchaseOrderStatus.Cancelled
            });

        update.IsSuccess.Should().BeTrue(update.Message);
        await using var db = CreateDbContext();
        var persisted = await db.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == order.Id);
        persisted.Status.Should().Be(PurchaseOrderStatus.Cancelled);

        missing.IsSuccess.Should().BeFalse();
        missing.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Test]
    [Category(TestCategories.Stock)]
    [Category(TestCategories.Reservations)]
    public async Task StockAndReservationReadWrappers_PostedAndReservedStock_ReturnExpectedRows()
    {
        var seed = await SeedInventoryScope(withLot: true);
        var movementKey = $"movement-read-{Guid.NewGuid():N}";
        var post = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(
            new PostStockMovementRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot!.Id,
                MovementType = InventoryMovementType.OpeningBalance,
                Quantity = 10,
                IdempotencyKey = movementKey
            });
        post.IsSuccess.Should().BeTrue(post.Message);

        var referenceId = Guid.NewGuid();
        var reserve = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id,
                Quantity = 3,
                ReferenceType = "wrapper-coverage",
                ReferenceId = referenceId
            });
        reserve.IsSuccess.Should().BeTrue(reserve.Message);

        await using var setupDb = CreateDbContext();
        var reservation = await setupDb.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReferenceId == referenceId);
        var allocation = await setupDb.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReservationId == reservation.Id);

        var balances = await InventarioIntegrationTestFixture.ServiceWrapper.GetStockBalances(
            new GetStockBalancesRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id
            });
        var movements = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryMovements(
            new GetInventoryMovementsRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id,
                IdempotencyKey = movementKey
            });
        var reservations = await InventarioIntegrationTestFixture.ServiceWrapper.GetReservations(
            new GetReservationsRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                Status = ReservationStatus.Active,
                ReferenceType = "wrapper-coverage",
                ReferenceId = referenceId
            });
        var allocations = await InventarioIntegrationTestFixture.ServiceWrapper.GetReservationAllocations(
            new GetReservationAllocationsRequest
            {
                Metadata = CreateMetadata(),
                ReservationId = reservation.Id,
                ProductId = seed.Product.Id,
                LotId = seed.Lot.Id,
                Status = ReservationAllocationStatus.Reserved
            });

        balances.IsSuccess.Should().BeTrue(balances.Message);
        balances.Response.Should().ContainSingle(x => x.ProductId == seed.Product.Id && x.ReservedQuantity == 3);

        movements.IsSuccess.Should().BeTrue(movements.Message);
        movements.Response.Should().ContainSingle(x => x.IdempotencyKey == movementKey);

        reservations.IsSuccess.Should().BeTrue(reservations.Message);
        reservations.Response.Should().ContainSingle(x => x.Id == reservation.Id);

        allocations.IsSuccess.Should().BeTrue(allocations.Message);
        allocations.Response.Should().ContainSingle(x => x.Id == allocation.Id);

        var cancel = await InventarioIntegrationTestFixture.ServiceWrapper.CancelReservation(
            new CancelReservationRequest
            {
                Metadata = CreateMetadata(),
                ReservationId = reservation.Id,
                Reason = "wrapper coverage cancel"
            });
        var missingCancel = await InventarioIntegrationTestFixture.ServiceWrapper.CancelReservation(
            new CancelReservationRequest
            {
                Metadata = CreateMetadata(),
                ReservationId = Guid.NewGuid(),
                Reason = "missing reservation"
            });

        cancel.IsSuccess.Should().BeTrue(cancel.Message);
        missingCancel.IsSuccess.Should().BeFalse();
        missingCancel.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private async Task<Product> SeedProduct()
    {
        await using var db = CreateDbContext();
        return await TestInventarioSeed.SeedProduct(db);
    }

    private async Task<InventoryScope> SeedInventoryScope(bool withLot = false)
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var lot = withLot
            ? await TestInventarioSeed.SeedLot(db, product.Id, expiresAt: DateTime.UtcNow.AddDays(120))
            : null;
        return new InventoryScope(product, warehouse, location, lot);
    }

    private async Task<Supplier> CreateSupplier()
    {
        var code = UniqueCode("SUP");
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateSupplier(
            new CreateSupplierRequest
            {
                Metadata = CreateMetadata(),
                Code = code,
                Name = $"Supplier {code}",
                IsActive = true
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<Supplier>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Code == code);
    }

    private async Task<PurchaseOrder> CreatePurchaseOrder(
        Guid productId,
        Guid supplierId,
        PurchaseOrderStatus status)
    {
        var orderNumber = UniqueCode("PO");
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreatePurchaseOrder(
            new CreatePurchaseOrderRequest
            {
                Metadata = CreateMetadata(),
                OrderNumber = orderNumber,
                SupplierId = supplierId,
                Status = status,
                Lines =
                [
                    new PurchaseOrderLineRequest
                    {
                        ProductId = productId,
                        OrderedQuantity = 10,
                        UnitCost = 5m,
                        UnitOfMeasure = "ea"
                    }
                ]
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Include(x => x.Lines)
            .FirstAsync(x => x.OrderNumber == orderNumber);
    }

    private async Task<ReceivingDocument> ReceiveInventory(
        InventoryScope seed,
        PurchaseOrder order)
    {
        var receiptNumber = UniqueCode("RCV");
        var line = order.Lines.Single();
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(
            new ReceiveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ReceiptNumber = receiptNumber,
                PurchaseOrderId = order.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                SupplierId = order.SupplierId,
                ReceivedAt = DateTime.UtcNow,
                IdempotencyKey = $"wrapper-receive-{Guid.NewGuid():N}",
                Lines =
                [
                    new ReceivingLineRequest
                    {
                        PurchaseOrderLineId = line.Id,
                        ProductId = seed.Product.Id,
                        Quantity = 2,
                        UnitCost = 5m,
                        UnitOfMeasure = "ea",
                        LotNumber = UniqueCode("LOT"),
                        ExpiresAt = DateTime.UtcNow.AddDays(180)
                    }
                ]
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<ReceivingDocument>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReceiptNumber == receiptNumber);
    }

    private static T WithBase<T>(T entity, Guid tenantId)
        where T : XFramework.Domain.Shared.Contracts.Base.BaseModel
    {
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.TenantId = tenantId;
        entity.IsEnabled = true;
        entity.CreatedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        return entity;
    }

    private sealed record InventoryScope(
        Product Product,
        Warehouse Warehouse,
        InventoryLocation Location,
        InventoryLot? Lot);
}
