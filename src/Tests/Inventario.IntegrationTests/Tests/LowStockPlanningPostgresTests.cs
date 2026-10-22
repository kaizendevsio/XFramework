using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace InventarioPlanningPostgres.IntegrationTests;

[TestFixture, NonParallelizable]
public sealed class LowStockPlanningPostgresTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder().WithDatabase("inventario_planning_tests")
            .WithUsername("test").WithPassword("test_password").Build();
        await _postgres.StartAsync();
        _ = typeof(RegistryConfiguration).Assembly;
        _ = typeof(Warehouse).Assembly;
        await using var db = Database(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (_postgres is not null) await _postgres.DisposeAsync(); }

    [Test]
    public async Task LowStock_CountAndPagesAggregateBeyondLegacyRuleAndBalanceCaps()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = new ProductCategory { Id = Guid.NewGuid(), TenantId = tenant, Name = "Planning", IsEnabled = true };
        var low = new Product { Id = Guid.NewGuid(), TenantId = tenant, CategoryId = category.Id, Name = "Low stock", IsEnabled = true };
        var stocked = new Product { Id = Guid.NewGuid(), TenantId = tenant, CategoryId = category.Id, Name = "Stocked", IsEnabled = true };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "MAIN", Name = "Main", IsEnabled = true };
        var otherWarehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "OTHER", Name = "Other", IsEnabled = true };
        db.AddRange(category, low, stocked, warehouse, otherWarehouse);
        for (var i = 0; i < 5001; i++)
        {
            var location = new InventoryLocation { Id = Guid.NewGuid(), TenantId = tenant, WarehouseId = warehouse.Id,
                Code = $"L{i}", Name = $"Location {i}", IsEnabled = true };
            db.Add(location);
            db.Add(new StockBalance { Id = Guid.NewGuid(), TenantId = tenant, ProductId = stocked.Id,
                WarehouseId = warehouse.Id, LocationId = location.Id, OnHandQuantity = 1, AvailableQuantity = 1, IsEnabled = true });
            if (i < 1201)
                db.Add(new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenant, ProductId = low.Id,
                    WarehouseId = warehouse.Id, LocationId = location.Id, ReorderPoint = 5, ReorderQuantity = 10,
                    PreferredSupplier = i % 2 == 0 ? "Alpha" : "Beta", IsActive = true, IsEnabled = true });
        }
        // A capped 5,000-balance sum would incorrectly classify this rule as low stock.
        db.Add(new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenant, ProductId = stocked.Id,
            WarehouseId = warehouse.Id, ReorderPoint = 5000, ReorderQuantity = 1, IsActive = true, IsEnabled = true });
        db.Add(new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenant, ProductId = low.Id,
            WarehouseId = otherWarehouse.Id, ReorderPoint = 5, ReorderQuantity = 10, IsActive = true, IsEnabled = true });
        db.Add(new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenant, ProductId = low.Id,
            ReorderPoint = 5, ReorderQuantity = 10, IsActive = false, IsEnabled = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var invocation = new Invocation(tenant);
        var data = new ServerDataContext<AppDbContext>(db);
        var gates = new Gates();
        var planning = new InventoryPlanningService(data, invocation, gates, new ProductVariationService(data, invocation, gates), db);
        var reporting = new InventoryReportingService(data, invocation, planning, gates, db);
        var count = await reporting.GetLowStockCountAsync(new() { WarehouseId = warehouse.Id });
        count.IsSuccess.Should().BeTrue(count.Message);
        count.Data.Should().Be(1201, "count measures matching reorder-rule positions, not distinct products");
        var first = await planning.GetReorderSuggestionPageAsync(new() { WarehouseId = warehouse.Id, Take = 100 });
        var last = await planning.GetReorderSuggestionPageAsync(new() { WarehouseId = warehouse.Id, Skip = 1200, Take = 100 });
        first.IsSuccess.Should().BeTrue(first.Message);
        first.Data!.TotalCount.Should().Be(1201);
        first.Data.TotalSuggestedQuantity.Should().Be(18015);
        first.Data.Items.Should().HaveCount(100).And.OnlyContain(x => x.ProductId == low.Id);
        last.Data!.Items.Should().ContainSingle();
        first.Data.Items.Select(x => x.LocationId).Should().NotContain(last.Data.Items[0].LocationId);
        var filtered = await planning.GetReorderSuggestionPageAsync(new() {
            WarehouseId = warehouse.Id, SupplierFilter = "alpha", SupplierFilterOperator = "Contains", Take = 20 });
        filtered.Data!.TotalCount.Should().Be(601);
        filtered.Data.TotalSuggestedQuantity.Should().Be(9015);
        var notAlpha = await planning.GetReorderSuggestionPageAsync(new() {
            WarehouseId = warehouse.Id, SupplierFilter = "ALPHA", SupplierFilterOperator = "NotContains", Take = 20 });
        notAlpha.IsSuccess.Should().BeTrue(notAlpha.Message);
        notAlpha.Data!.TotalCount.Should().Be(600);
        notAlpha.Data.TotalSuggestedQuantity.Should().Be(9000);
        notAlpha.Data.Items.Should().OnlyContain(x => x.PreferredSupplier == "Beta");
        var noSupplier = await planning.GetReorderSuggestionPageAsync(new() {
            WarehouseId = otherWarehouse.Id, SupplierFilter = "ALPHA", SupplierFilterOperator = "NotContains", Take = 20 });
        noSupplier.Data!.TotalCount.Should().Be(1);
        (await planning.GetReorderSuggestionsAsync(new() { WarehouseId = warehouse.Id })).Data.Should().HaveCount(1000);
        (await reporting.GetLowStockAsync(new() { WarehouseId = warehouse.Id })).Data.Should().HaveCount(1000);
        (await reporting.GetLowStockCountAsync(new() { WarehouseId = otherWarehouse.Id })).Data.Should().Be(1);
        (await reporting.GetLowStockCountAsync(new() { ProductId = stocked.Id })).Data.Should().Be(0);

        var foreign = new Invocation(Guid.NewGuid());
        var foreignPlanning = new InventoryPlanningService(data, foreign, gates, new ProductVariationService(data, foreign, gates), db);
        (await foreignPlanning.GetReorderSuggestionPageAsync(new() { WarehouseId = warehouse.Id })).StatusCode.Should().Be(404);
        (await foreignPlanning.GetReorderSuggestionPageAsync(new())).Data!.TotalCount.Should().Be(0);
    }

    private AppDbContext Database(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning, RelationalEventId.BoolWithDefaultWarning)).Options,
        new HttpContextAccessor(), new ConfigurationBuilder().Build(), new Invocation(tenant));

    private sealed class Invocation(Guid tenant) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext Current { get; } = new(new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string> { "inventario.planning:view", "inventario.reporting:view" }, "test", DateTimeOffset.UtcNow.AddHours(1)),
            null, tenant, null, Guid.NewGuid());
    }

    private sealed class Gates : ITenantModuleFeatureService
    {
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) => Task.FromResult(Result.Success());
        public Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) => Task.FromResult(Result<bool>.Success(true));
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) { }
    }
}
