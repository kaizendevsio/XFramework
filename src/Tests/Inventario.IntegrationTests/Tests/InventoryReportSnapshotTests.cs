using System.Net;
using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Reporting"), Category("Area:Wrappers")]
public sealed class InventoryReportSnapshotTests : InventarioTestBase
{
    [Test]
    public async Task GetInventoryReportSnapshot_PostgresFiltersAndAggregates_UsesReadOnlyWrapper()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db, name: "Synthetic reporting product");
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var lot = await TestInventarioSeed.SeedLot(db, product.Id, expiresAt: DateTime.UtcNow.AddDays(5));
        var tenant = InventarioIntegrationTestFixture.TestTenantId;
        var now = DateTime.UtcNow;
        db.Set<StockBalance>().Add(new()
        {
            Id = Guid.NewGuid(), TenantId = tenant, ProductId = product.Id, WarehouseId = warehouse.Id,
            LocationId = location.Id, LotId = lot.Id, OnHandQuantity = 12, ReservedQuantity = 2,
            AvailableQuantity = 10, IsEnabled = true, CreatedAt = now
        });
        foreach (var (quantity, date) in new[] { (12m, now.AddDays(-2)), (-2m, now.AddDays(-1)), (100m, now.AddDays(-80)) })
            db.Set<InventoryMovement>().Add(new()
            {
                Id = Guid.NewGuid(), TenantId = tenant, ProductId = product.Id, WarehouseId = warehouse.Id,
                LocationId = location.Id, LotId = lot.Id, MovementType = InventoryMovementType.Adjustment,
                QuantityDelta = quantity, MovementDate = date, IsEnabled = true, CreatedAt = now,
                IdempotencyKey = "synthetic-report-" + Guid.NewGuid()
            });
        foreach (var (type, quantity) in new[] { (InventoryMovementType.Reservation, 5m), (InventoryMovementType.Release, -5m) })
            db.Set<InventoryMovement>().Add(new() {
                Id = Guid.NewGuid(), TenantId = tenant, ProductId = product.Id, WarehouseId = warehouse.Id,
                LocationId = location.Id, MovementType = type, QuantityDelta = quantity, MovementDate = now.AddHours(-1),
                IsEnabled = true, CreatedAt = now, IdempotencyKey = "synthetic-report-" + Guid.NewGuid()
            });
        await db.SaveChangesAsync();
        var countBefore = await db.Set<InventoryMovement>().CountAsync(x => x.ProductId == product.Id);
        var request = new GetInventoryReportSnapshotRequest
        {
            Metadata = CreateMetadata(), ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id,
            FromUtc = now.AddDays(-30), ToUtc = now, DaysAhead = 10
        };
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryReportSnapshot(request);
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Response!.TenantId.Should().Be(tenant);
        result.Response.OnHand.Should().Be(12);
        result.Response.Reserved.Should().Be(2);
        result.Response.Available.Should().Be(10);
        result.Response.Inbound.Should().Be(12);
        result.Response.Outbound.Should().Be(2);
        result.Response.MovementTimeline.Should().HaveCount(31);
        result.Response.MovementTimeline.Should().Contain(x => x.DateUtc == now.AddDays(-2).Date && x.Inbound == 12 && x.Outbound == 0);
        result.Response.MovementTimeline.Should().Contain(x => x.DateUtc == now.AddDays(-1).Date && x.Inbound == 0 && x.Outbound == 2);
        result.Response.MovementTimeline.Should().Contain(x => x.DateUtc == now.AddDays(-3).Date && x.Inbound == 0 && x.Outbound == 0);
        result.Response.MovementCount.Should().Be(4);
        result.Response.Positions.Should().ContainSingle();
        result.Response.NearExpiry.Should().ContainSingle(x => x.LotId == lot.Id);
        (await db.Set<InventoryMovement>().CountAsync(x => x.ProductId == product.Id)).Should().Be(countBefore);

        var invalid = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryReportSnapshot(
            request with { FromUtc = now.AddDays(1) });
        invalid.IsSuccess.Should().BeFalse();
        invalid.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        var forged = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryReportSnapshot(
            request with { Metadata = new() { RequestedTenantId = Guid.NewGuid() } });
        forged.IsSuccess.Should().BeFalse();
        forged.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetInventoryReportSnapshot_TimelineIncludesRowsBeyondDetailCap_AndUsesUtcDays()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db, name: "Timeline cap product");
        var warehouse = await TestInventarioSeed.SeedWarehouse(db);
        var location = await TestInventarioSeed.SeedLocation(db, warehouse.Id);
        var day = DateTime.UtcNow.Date.AddDays(-3);
        var tenant = InventarioIntegrationTestFixture.TestTenantId;
        for (var i = 0; i < 1005; i++)
            db.Set<InventoryMovement>().Add(new()
            {
                Id = Guid.NewGuid(), TenantId = tenant, ProductId = product.Id,
                WarehouseId = warehouse.Id, LocationId = location.Id,
                MovementType = InventoryMovementType.Adjustment,
                QuantityDelta = i < 1001 ? 2 : -3,
                MovementDate = i < 1001 ? day.AddHours(23).AddMinutes(59) : day.AddDays(1),
                IsEnabled = true, CreatedAt = day, IdempotencyKey = "timeline-cap-" + Guid.NewGuid()
            });
        await db.SaveChangesAsync();
        var result = await InventarioIntegrationTestFixture.ServiceWrapper.GetInventoryReportSnapshot(new()
        {
            Metadata = CreateMetadata(), ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id,
            FromUtc = day, ToUtc = day.AddDays(2), DaysAhead = 10
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        var snapshot = result.Response!;
        snapshot.Movements.Should().HaveCount(1000);
        snapshot.MovementCount.Should().Be(1005);
        snapshot.MovementTimeline.Should().HaveCount(3);
        snapshot.MovementTimeline[0].DateUtc.Should().Be(day);
        snapshot.MovementTimeline[0].Inbound.Should().Be(2002);
        snapshot.MovementTimeline[1].DateUtc.Should().Be(day.AddDays(1));
        snapshot.MovementTimeline[1].Outbound.Should().Be(12);
        snapshot.MovementTimeline[2].DateUtc.Should().Be(day.AddDays(2));
        snapshot.MovementTimeline[2].Inbound.Should().Be(0);
        snapshot.MovementTimeline[2].Outbound.Should().Be(0);
        snapshot.Inbound.Should().Be(2002);
        snapshot.Outbound.Should().Be(12);
        snapshot.MovementTimeline.Should().OnlyContain(x => x.Inbound >= 0 && x.Outbound >= 0);
    }
}
