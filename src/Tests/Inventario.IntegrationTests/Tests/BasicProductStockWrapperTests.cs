using System.Net;
using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture, NonParallelizable]
[Category(TestCategories.Integration), Category(TestCategories.Inventario), Category(TestCategories.Wrappers)]
public sealed class BasicProductStockWrapperTests : InventarioTestBase
{
    [Test]
    public async Task PostBasicProductStock_GeneratedWrapper_ReceivesReplaysAndRejectsChangedQuantity()
    {
        await using var seed = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(seed);
        var warehouse = await TestInventarioSeed.SeedWarehouse(seed);
        var location = await TestInventarioSeed.SeedLocation(seed, warehouse.Id);
        var tenant = InventarioIntegrationTestFixture.TestTenantId;
        var setup = await seed.Set<InventarioSetup>().AsTracking().SingleOrDefaultAsync(x => x.TenantId == tenant);
        var added = setup is null;
        setup ??= new InventarioSetup { Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true };
        var original = (setup.Mode, setup.WarehouseId, setup.LocationId);
        if (added) seed.Add(setup);
        setup.Mode = InventarioSetupMode.Basic;
        setup.WarehouseId = warehouse.Id;
        setup.LocationId = location.Id;
        await seed.SaveChangesAsync();
        try
        {
            var request = new PostBasicProductStockRequest
            {
                Metadata = CreateMetadata(), ProductId = product.Id, Quantity = 2.5m,
                Reason = "Wrapper receipt", IdempotencyKey = Guid.NewGuid().ToString()
            };
            var wrapper = InventarioIntegrationTestFixture.ServiceWrapper;
            var first = await wrapper.PostBasicProductStock(request);
            first.IsSuccess.Should().BeTrue(first.Message);
            first.Response!.OnHandQuantity.Should().Be(2.5m);
            first.Response.WarehouseId.Should().Be(warehouse.Id);
            first.Response.IsIdempotentReplay.Should().BeFalse();
            var replay = await wrapper.PostBasicProductStock(request);
            replay.IsSuccess.Should().BeTrue(replay.Message);
            replay.Response!.IsIdempotentReplay.Should().BeTrue();
            var conflict = await wrapper.PostBasicProductStock(request with { Quantity = 3 });
            conflict.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
            var invalid = await wrapper.PostBasicProductStock(request with { Quantity = 0, IdempotencyKey = Guid.NewGuid().ToString() });
            invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
            await using var read = CreateDbContext();
            (await read.Set<Product>().Where(x => x.Id == product.Id).Select(x => x.StockQuantity).SingleAsync()).Should().Be(2.5m);
            (await read.Set<InventoryMovement>().CountAsync(x => x.IdempotencyKey == request.IdempotencyKey)).Should().Be(1);
        }
        finally
        {
            if (added) seed.Remove(setup);
            else (setup.Mode, setup.WarehouseId, setup.LocationId) = original;
            await seed.SaveChangesAsync();
        }
    }
}
