using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
public sealed class WarehousingTraceabilityTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.Warehousing)]
    public async Task CreateWarehouse_ValidRequest_PersistsWarehouseAndLocation()
    {
        var code = UniqueCode("WH");

        var warehouseResult = await InventarioIntegrationTestFixture.ServiceWrapper.CreateWarehouse(
            new CreateWarehouseRequest
            {
                Metadata = CreateMetadata(),
                Code = code,
                Name = "Integration Warehouse"
            });

        warehouseResult.IsSuccess.Should().BeTrue(warehouseResult.Message);
        await using var db = CreateDbContext();
        var warehouse = await db.Set<Warehouse>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Code == code);
        warehouse.Should().NotBeNull();

        var locationCode = UniqueCode("BIN");
        var locationResult = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryLocation(
            new CreateInventoryLocationRequest
            {
                Metadata = CreateMetadata(),
                WarehouseId = warehouse!.Id,
                Code = locationCode,
                Name = "Integration Bin",
                LocationType = InventoryLocationType.Bin,
                IsPickable = true
            });

        locationResult.IsSuccess.Should().BeTrue(locationResult.Message);
        var location = await db.Set<InventoryLocation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.Code == locationCode);
        location.Should().NotBeNull();
        location!.WarehouseId.Should().Be(warehouse.Id);
    }

    [Test]
    [Category(TestCategories.Traceability)]
    public async Task CreateInventoryLot_ValidRequest_PersistsLotForProduct()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);

        var lotNumber = UniqueCode("LOT");
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryLot(
            new CreateInventoryLotRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                LotNumber = lotNumber,
                SupplierReference = "supplier-batch",
                ReceivedAt = DateTime.UtcNow,
                ManufacturedAt = DateTime.UtcNow.AddDays(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(90),
                UnitCost = 6m,
                Status = InventoryLotStatus.Available
            });

        result.IsSuccess.Should().BeTrue(result.Message);

        var persisted = await db.Set<InventoryLot>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProductId == product.Id && x.LotNumber == lotNumber);
        persisted.Should().NotBeNull();
        persisted!.ProductId.Should().Be(product.Id);
    }

    [Test]
    [Category(TestCategories.Traceability)]
    public async Task CreateInventoryLot_SameProductAndLotNumber_ReturnsConflict()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var lotNumber = UniqueCode("LOT");
        var request = new CreateInventoryLotRequest
        {
            Metadata = CreateMetadata(),
            ProductId = product.Id,
            LotNumber = lotNumber,
            ReceivedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            Status = InventoryLotStatus.Available
        };

        var first = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryLot(request);
        var duplicate = await InventarioIntegrationTestFixture.ServiceWrapper.CreateInventoryLot(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        duplicate.IsSuccess.Should().BeFalse();
        duplicate.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var lotCount = await db.Set<InventoryLot>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductId == product.Id && x.LotNumber == lotNumber);
        lotCount.Should().Be(1);
    }
}
