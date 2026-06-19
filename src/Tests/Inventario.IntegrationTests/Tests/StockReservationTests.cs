using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
public sealed class StockReservationTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.Stock)]
    public async Task PostStockMovement_OpeningBalance_CreatesMovementAndBalance()
    {
        var seed = await SeedInventoryScope();

        var result = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(
            new PostStockMovementRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id,
                MovementType = InventoryMovementType.OpeningBalance,
                Quantity = 25,
                IdempotencyKey = $"stock-{Guid.NewGuid():N}"
            });

        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        var balance = await db.Set<StockBalance>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.ProductId == seed.Product.Id &&
                x.WarehouseId == seed.Warehouse.Id &&
                x.LocationId == seed.Location.Id &&
                x.LotId == seed.Lot.Id);
        balance.Should().NotBeNull();
        balance!.OnHandQuantity.Should().Be(25);
        balance.AvailableQuantity.Should().Be(25);

        var movement = await db.Set<InventoryMovement>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.StockBalanceId == balance.Id);
        movement.Should().NotBeNull();
        movement!.LotId.Should().Be(seed.Lot.Id);
    }

    [Test]
    [Category(TestCategories.Stock)]
    public async Task PostStockMovement_SameIdempotencyKeyAndPayload_ReplaysWithoutDoublePosting()
    {
        var seed = await SeedInventoryScope();
        var key = $"stock-idem-{Guid.NewGuid():N}";
        var request = new PostStockMovementRequest
        {
            Metadata = CreateMetadata(),
            ProductId = seed.Product.Id,
            WarehouseId = seed.Warehouse.Id,
            LocationId = seed.Location.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 10,
            IdempotencyKey = key
        };

        var first = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(request);
        var replay = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        replay.IsSuccess.Should().BeTrue(replay.Message);

        await using var db = CreateDbContext();
        var movementCount = await db.Set<InventoryMovement>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.IdempotencyKey == key);
        movementCount.Should().Be(1);
    }

    [Test]
    [Category(TestCategories.Stock)]
    public async Task PostStockMovement_SameIdempotencyKeyDifferentPayload_ReturnsConflict()
    {
        var seed = await SeedInventoryScope();
        var key = $"stock-conflict-{Guid.NewGuid():N}";
        var request = new PostStockMovementRequest
        {
            Metadata = CreateMetadata(),
            ProductId = seed.Product.Id,
            WarehouseId = seed.Warehouse.Id,
            LocationId = seed.Location.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 10,
            IdempotencyKey = key
        };

        var first = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(request);
        var conflict = await InventarioIntegrationTestFixture.ServiceWrapper.PostStockMovement(request with { Quantity = 11 });

        first.IsSuccess.Should().BeTrue(first.Message);
        conflict.IsSuccess.Should().BeFalse();
        conflict.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Test]
    [Category(TestCategories.Reservations)]
    public async Task ReserveInventory_LotOmitted_AllocatesEarliestExpiryFirst()
    {
        await using var setupDb = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(setupDb);
        var product = await TestInventarioSeed.SeedProduct(setupDb, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(setupDb);
        var location = await TestInventarioSeed.SeedLocation(setupDb, warehouse.Id);
        var laterLot = await TestInventarioSeed.SeedLot(setupDb, product.Id, expiresAt: DateTime.UtcNow.AddDays(60));
        var earlierLot = await TestInventarioSeed.SeedLot(setupDb, product.Id, expiresAt: DateTime.UtcNow.AddDays(15));

        await PostOpeningBalance(product.Id, warehouse.Id, location.Id, laterLot.Id, 10);
        await PostOpeningBalance(product.Id, warehouse.Id, location.Id, earlierLot.Id, 10);

        var referenceId = Guid.NewGuid();
        var reservation = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                LocationId = location.Id,
                Quantity = 6,
                ReferenceType = "integration-test",
                ReferenceId = referenceId
            });

        reservation.IsSuccess.Should().BeTrue(reservation.Message);

        await using var assertDb = CreateDbContext();
        var persistedReservation = await assertDb.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ReferenceId == referenceId);
        persistedReservation.Should().NotBeNull();

        var allocations = await assertDb.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .Where(x => x.ReservationId == persistedReservation!.Id)
            .ToListAsync();

        allocations.Should().ContainSingle();
        allocations[0].LotId.Should().Be(earlierLot.Id);
        allocations[0].Quantity.Should().Be(6);
    }

    [Test]
    [Category(TestCategories.Reservations)]
    public async Task ReserveInventory_OnlyExpiredLotWithoutOverride_ReturnsConflict()
    {
        await using var setupDb = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(setupDb);
        var product = await TestInventarioSeed.SeedProduct(setupDb, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(setupDb);
        var location = await TestInventarioSeed.SeedLocation(setupDb, warehouse.Id);
        var expiredLot = await TestInventarioSeed.SeedLot(setupDb, product.Id, expiresAt: DateTime.UtcNow.AddDays(-1));

        await PostOpeningBalance(product.Id, warehouse.Id, location.Id, expiredLot.Id, 10);

        var reservation = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                LocationId = location.Id,
                Quantity = 3,
                ReferenceType = "expired-lot-test",
                ReferenceId = Guid.NewGuid()
            });

        reservation.IsSuccess.Should().BeFalse();
        reservation.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        await using var assertDb = CreateDbContext();
        var allocationCount = await assertDb.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductId == product.Id);
        allocationCount.Should().Be(0);
    }

    [Test]
    [Category(TestCategories.Reservations)]
    public async Task ReleaseReservation_ActiveReservation_ReleasesAllocationAndAvailability()
    {
        var seed = await SeedInventoryScope();
        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, seed.Lot.Id, 10);
        var reservationId = await ReserveAndGetId(seed, quantity: 4);

        var release = await InventarioIntegrationTestFixture.ServiceWrapper.ReleaseReservation(
            new ReleaseReservationRequest
            {
                Metadata = CreateMetadata(),
                ReservationId = reservationId,
                Reason = "integration release"
            });

        release.IsSuccess.Should().BeTrue(release.Message);

        await using var db = CreateDbContext();
        var reservation = await db.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == reservationId);
        reservation.Status.Should().Be(ReservationStatus.Released);

        var allocation = await db.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReservationId == reservationId);
        allocation.Status.Should().Be(ReservationAllocationStatus.Released);

        var balance = await db.Set<StockBalance>()
            .IgnoreQueryFilters()
            .FirstAsync(x =>
                x.ProductId == seed.Product.Id &&
                x.WarehouseId == seed.Warehouse.Id &&
                x.LocationId == seed.Location.Id &&
                x.LotId == seed.Lot.Id);
        balance.ReservedQuantity.Should().Be(0);
        balance.AvailableQuantity.Should().Be(10);
    }

    [Test]
    [Category(TestCategories.Reservations)]
    public async Task FulfillReservation_ActiveReservation_PostsShipmentAndCompletesAllocation()
    {
        var seed = await SeedInventoryScope();
        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, seed.Lot.Id, 10);
        var reservationId = await ReserveAndGetId(seed, quantity: 4);

        var fulfill = await InventarioIntegrationTestFixture.ServiceWrapper.FulfillReservation(
            new FulfillReservationRequest
            {
                Metadata = CreateMetadata(),
                ReservationId = reservationId,
                Reason = "integration fulfill"
            });

        fulfill.IsSuccess.Should().BeTrue(fulfill.Message);

        await using var db = CreateDbContext();
        var reservation = await db.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == reservationId);
        reservation.Status.Should().Be(ReservationStatus.Fulfilled);

        var allocation = await db.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReservationId == reservationId);
        allocation.Status.Should().Be(ReservationAllocationStatus.Fulfilled);
        allocation.FulfilledAt.Should().NotBeNull();

        var shipment = await db.Set<InventoryMovement>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.ReferenceId == reservationId &&
                x.MovementType == InventoryMovementType.Shipment);
        shipment.Should().NotBeNull();

        var balance = await db.Set<StockBalance>()
            .IgnoreQueryFilters()
            .FirstAsync(x =>
                x.ProductId == seed.Product.Id &&
                x.WarehouseId == seed.Warehouse.Id &&
                x.LocationId == seed.Location.Id &&
                x.LotId == seed.Lot.Id);
        balance.OnHandQuantity.Should().Be(6);
        balance.AvailableQuantity.Should().Be(6);
    }

    [Test]
    [Category(TestCategories.Reservations)]
    public async Task ExpireReservations_ExpiredActiveReservation_ExpiresAllocation()
    {
        var seed = await SeedInventoryScope();
        await PostOpeningBalance(seed.Product.Id, seed.Warehouse.Id, seed.Location.Id, seed.Lot.Id, 10);
        var reservationId = await ReserveAndGetId(
            seed,
            quantity: 2,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var expired = await InventarioIntegrationTestFixture.ServiceWrapper.ExpireReservations(
            new ExpireReservationsRequest
            {
                Metadata = CreateMetadata(),
                ExpiresBefore = DateTime.UtcNow,
                MaxCount = 10
            });

        expired.IsSuccess.Should().BeTrue(expired.Message);

        await using var db = CreateDbContext();
        var reservation = await db.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == reservationId);
        reservation.Status.Should().Be(ReservationStatus.Expired);

        var allocation = await db.Set<ReservationAllocation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReservationId == reservationId);
        allocation.Status.Should().Be(ReservationAllocationStatus.Expired);
    }

    private async Task<InventoryScope> SeedInventoryScope()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var lot = await TestInventarioSeed.SeedLot(db, product.Id, expiresAt: DateTime.UtcNow.AddDays(90));
        return new InventoryScope(product, warehouse, location, lot);
    }

    private static async Task PostOpeningBalance(
        Guid productId,
        Guid warehouseId,
        Guid locationId,
        Guid lotId,
        decimal quantity)
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
                IdempotencyKey = $"seed-stock-{Guid.NewGuid():N}"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private async Task<Guid> ReserveAndGetId(
        InventoryScope seed,
        decimal quantity,
        DateTime? expiresAt = null)
    {
        var referenceId = Guid.NewGuid();
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.ReserveInventory(
            new ReserveInventoryRequest
            {
                Metadata = CreateMetadata(),
                ProductId = seed.Product.Id,
                WarehouseId = seed.Warehouse.Id,
                LocationId = seed.Location.Id,
                LotId = seed.Lot.Id,
                Quantity = quantity,
                ExpiresAt = expiresAt,
                ReferenceType = "reservation-lifecycle-test",
                ReferenceId = referenceId
            });

        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        var reservation = await db.Set<Reservation>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.ReferenceId == referenceId);
        return reservation.Id;
    }

    private sealed record InventoryScope(
        Product Product,
        Warehouse Warehouse,
        InventoryLocation Location,
        InventoryLot Lot);
}
