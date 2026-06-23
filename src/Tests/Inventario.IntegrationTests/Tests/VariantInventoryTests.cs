using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Inventario)]
public sealed class VariantInventoryTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.Traceability)]
    [Category(TestCategories.Stock)]
    [Category(TestCategories.Wrappers)]
    public async Task LotsAndStockMovements_VariantDimension_AllowSameLotNumberAndRejectMismatchedLot()
    {
        var seed = await SeedInventoryScope();
        var sizeType = await CreateVariationType("Size");
        var small = await CreateVariant(seed.Product, sizeType.Id, "Small", 11m);
        var large = await CreateVariant(seed.Product, sizeType.Id, "Large", 13m);
        var lotNumber = UniqueCode("LOT");

        var smallLot = await CreateLot(seed.Product.Id, small.Id, lotNumber);
        var largeLot = await CreateLot(seed.Product.Id, large.Id, lotNumber);

        smallLot.ProductVariationId.Should().Be(small.Id);
        largeLot.ProductVariationId.Should().Be(large.Id);
        largeLot.Id.Should().NotBe(smallLot.Id);

        var mismatched = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(
            new PostStockMovementRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = small.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = largeLot.Id,
                MovementType = InventoryMovementType.OpeningBalance,
                Quantity = 5,
                IdempotencyKey = $"variant-lot-mismatch-{Guid.NewGuid():N}"
            });

        mismatched.IsSuccess.Should().BeFalse();
        mismatched.Message.Should().Contain("Lot does not belong to the requested variant");
    }

    [Test]
    [Category(TestCategories.Stock)]
    [Category(TestCategories.Wrappers)]
    public async Task StockBalances_VariantDimension_SegregateQuantitiesAndIdempotencyHashIncludesVariant()
    {
        var seed = await SeedInventoryScope();
        var colorType = await CreateVariationType("Color");
        var red = await CreateVariant(seed.Product, colorType.Id, "Red", 12m);
        var blue = await CreateVariant(seed.Product, colorType.Id, "Blue", 12m);
        var sharedKey = $"variant-idem-{Guid.NewGuid():N}";

        var redPost = await PostOpeningBalance(seed, red.Id, 8, sharedKey);
        var blueConflict = await PostOpeningBalance(seed, blue.Id, 4, sharedKey);
        var bluePost = await PostOpeningBalance(seed, blue.Id, 4, $"variant-stock-{Guid.NewGuid():N}");

        redPost.IsSuccess.Should().BeTrue(redPost.Message);
        blueConflict.IsSuccess.Should().BeFalse();
        blueConflict.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        bluePost.IsSuccess.Should().BeTrue(bluePost.Message);

        await using var db = CreateDbContext();
        var balances = await db.Set<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.ProductId == seed.Product.Id &&
                x.WarehouseId == seed.Warehouse.Id &&
                x.LocationId == seed.Location.Id)
            .ToListAsync();

        balances.Should().ContainSingle(x => x.ProductVariationId == red.Id && x.OnHandQuantity == 8);
        balances.Should().ContainSingle(x => x.ProductVariationId == blue.Id && x.OnHandQuantity == 4);

        var redBalances = await InventarioIntegrationTestFixture.ServiceWrapper.GetStockBalances(
            new GetStockBalancesRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = red.Id
            });

        redBalances.IsSuccess.Should().BeTrue(redBalances.Message);
        redBalances.Response.Should().ContainSingle(x => x.ProductVariationId == red.Id && x.OnHandQuantity == 8);
        redBalances.Response.Should().NotContain(x => x.ProductVariationId == blue.Id);
    }

    [Test]
    [Category(TestCategories.Purchasing)]
    [Category(TestCategories.Reservations)]
    [Category(TestCategories.Reporting)]
    [Category(TestCategories.Wrappers)]
    public async Task ReceivingReservationsAndReports_VariantDimension_PreserveAndFilterVariantIdentity()
    {
        var seed = await SeedInventoryScope();
        var flavorType = await CreateVariationType("Flavor");
        var vanilla = await CreateVariant(seed.Product, flavorType.Id, "Vanilla", 14m);
        var chocolate = await CreateVariant(seed.Product, flavorType.Id, "Chocolate", 14m);
        var supplier = await CreateSupplier();
        var order = await CreatePurchaseOrder(seed.Product.Id, vanilla.Id, supplier.Id);
        var line = order.Lines.Single();
        var receiptNumber = UniqueCode("RCV");

        var receipt = await InventarioIntegrationTestFixture.ServiceWrapper.ReceiveInventory(
            new ReceiveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ReceiptNumber = receiptNumber,
                PurchaseOrderId = order.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                SupplierId = supplier.Id,
                ReceivedAt = DateTime.UtcNow,
                IdempotencyKey = $"variant-receive-{Guid.NewGuid():N}",
                Lines =
                [
                    new ReceivingLineRequest
                    {
                        PurchaseOrderLineId = line.Id,
                        ProductId = seed.Product.Id,
                        ProductVariationId = vanilla.Id,
                        Quantity = 6,
                        UnitCost = 7m,
                        UnitOfMeasure = "ea"
                    }
                ]
            });
        receipt.IsSuccess.Should().BeTrue(receipt.Message);

        var wrongVariantReserve = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = chocolate.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                Quantity = 1,
                ReferenceType = "variant-wrong",
                ReferenceId = Guid.NewGuid()
            });
        wrongVariantReserve.IsSuccess.Should().BeFalse();

        var referenceId = Guid.NewGuid();
        var reserve = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = vanilla.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                Quantity = 2,
                ReferenceType = "variant-reserve",
                ReferenceId = referenceId
            });
        reserve.IsSuccess.Should().BeTrue(reserve.Message);

        await using var db = CreateDbContext();
        var persistedLine = await db.Set<PurchaseOrderLine>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == line.Id);
        persistedLine.ProductVariationId.Should().Be(vanilla.Id);

        var receivingLine = await db.Set<ReceivingLine>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ProductId == seed.Product.Id && x.ProductVariationId == vanilla.Id);
        receivingLine.ProductVariationId.Should().Be(vanilla.Id);

        var reservation = await db.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReferenceId == referenceId);
        reservation.ProductVariationId.Should().Be(vanilla.Id);

        var allocation = await db.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReservationId == reservation.Id);
        allocation.ProductVariationId.Should().Be(vanilla.Id);

        var stockReport = await InventarioIntegrationTestFixture.ServiceWrapper.GetStockPositionReport(
            new GetStockPositionReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = vanilla.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id
            });
        stockReport.IsSuccess.Should().BeTrue(stockReport.Message);
        stockReport.Response.Should().ContainSingle(x =>
            x.ProductVariationId == vanilla.Id &&
            x.ProductVariationName == "Vanilla" &&
            x.ProductVariationTypeName == flavorType.Name &&
            x.OnHandQuantity == 6 &&
            x.ReservedQuantity == 2);
        stockReport.Response.Should().NotContain(x => x.ProductVariationId == chocolate.Id);

        var allocationReport = await InventarioIntegrationTestFixture.ServiceWrapper.GetReservationAllocationStatusReport(
            new GetReservationAllocationStatusReportRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = vanilla.Id,
                Status = ReservationAllocationStatus.Reserved
            });
        allocationReport.IsSuccess.Should().BeTrue(allocationReport.Message);
        allocationReport.Response.Should().ContainSingle(x =>
            x.ProductVariationId == vanilla.Id &&
            x.ProductVariationName == "Vanilla" &&
            x.ProductVariationTypeName == flavorType.Name &&
            x.Quantity == 2);
    }

    private async Task<InventoryScope> SeedInventoryScope()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        return new InventoryScope(product, warehouse, location);
    }

    private async Task<ProductVariationType> CreateVariationType(string namePrefix)
    {
        var name = $"{namePrefix} {Guid.NewGuid():N}";
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariationType(
            new CreateProductVariationTypeRequest
            {
                Metadata = CreateMetadata(),
                Name = name
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<ProductVariationType>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Name == name);
    }

    private async Task<ProductVariation> CreateVariant(
        Product product,
        Guid variationTypeId,
        string name,
        decimal price)
    {
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateProductVariation(
            new CreateProductVariationRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                ProductVariationTypeId = variationTypeId,
                Name = name,
                Price = price
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ProductId == product.Id && x.ProductVariationTypeId == variationTypeId && x.Name == name);
    }

    private async Task<InventoryLot> CreateLot(
        Guid productId,
        Guid productVariationId,
        string lotNumber)
    {
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryLot(
            new CreateInventoryLotRequest
            {
                Metadata = CreateMetadata(),
                ProductId = productId,
                ProductVariationId = productVariationId,
                LotNumber = lotNumber,
                ReceivedAt = DateTime.UtcNow,
                Status = InventoryLotStatus.Available
            });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<InventoryLot>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ProductId == productId && x.ProductVariationId == productVariationId && x.LotNumber == lotNumber);
    }

    private static Task<XFramework.Domain.Shared.BusinessObjects.CmdResponse> PostOpeningBalance(
        InventoryScope seed,
        Guid productVariationId,
        decimal quantity,
        string idempotencyKey) =>
        InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(
            new PostStockMovementRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                ProductVariationId = productVariationId,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                MovementType = InventoryMovementType.OpeningBalance,
                Quantity = quantity,
                IdempotencyKey = idempotencyKey
            });

    private async Task<Supplier> CreateSupplier()
    {
        var supplierCode = UniqueCode("SUP");
        var supplier = await InventarioIntegrationTestFixture.ServiceWrapper.CreateSupplier(
            new CreateSupplierRequest
            {
                Metadata = CreateMetadata(),
                Code = supplierCode,
                Name = "Variant Supplier",
                IsActive = true
            });
        supplier.IsSuccess.Should().BeTrue(supplier.Message);

        await using var db = CreateDbContext();
        return await db.Set<Supplier>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Code == supplierCode);
    }

    private async Task<PurchaseOrder> CreatePurchaseOrder(
        Guid productId,
        Guid productVariationId,
        Guid supplierId)
    {
        var orderNumber = UniqueCode("PO");
        var order = await InventarioIntegrationTestFixture.ServiceWrapper.CreatePurchaseOrder(
            new CreatePurchaseOrderRequest
            {
                Metadata = CreateMetadata(),
                OrderNumber = orderNumber,
                SupplierId = supplierId,
                Status = PurchaseOrderStatus.Open,
                Lines =
                [
                    new PurchaseOrderLineRequest
                    {
                        ProductId = productId,
                        ProductVariationId = productVariationId,
                        OrderedQuantity = 10,
                        UnitCost = 7m,
                        UnitOfMeasure = "ea"
                    }
                ]
            });
        order.IsSuccess.Should().BeTrue(order.Message);

        await using var db = CreateDbContext();
        return await db.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Include(x => x.Lines)
            .FirstAsync(x => x.OrderNumber == orderNumber);
    }

    private sealed record InventoryScope(
        Product Product,
        Warehouse Warehouse,
        InventoryLocation Location);
}
