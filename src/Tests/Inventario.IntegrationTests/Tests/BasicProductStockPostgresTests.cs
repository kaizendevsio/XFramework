using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

// Own PostgreSQL only; deliberately outside the deployed-style Bolt fixture namespace.
namespace InventarioBasicStock.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Stock")]
public sealed class BasicProductStockPostgresTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder().WithDatabase("inventario_basic_stock_tests").WithUsername("test").WithPassword("test_password").Build();
        await _postgres.StartAsync();
        _ = typeof(RegistryConfiguration).Assembly;
        _ = typeof(Warehouse).Assembly;
        await using var db = Database(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (_postgres is not null) await _postgres.DisposeAsync(); }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Post_ConcurrentReceipts_ProvisionOnceAndSerializeSnapshot(bool sameKey)
    {
        var tenant = Guid.NewGuid();
        var request = await Seed(tenant);
        await using var first = Database(tenant);
        await using var second = Database(tenant);
        var other = sameKey ? request : request with { IdempotencyKey = Guid.NewGuid().ToString() };
        var results = await Task.WhenAll(Service(first, tenant).PostAsync(request), Service(second, tenant).PostAsync(other));
        results.Should().OnlyContain(x => x.IsSuccess);
        results.Count(x => x.Data!.IsIdempotentReplay).Should().Be(sameKey ? 1 : 0);
        await using var read = Database(tenant);
        (await read.Set<Warehouse>().CountAsync()).Should().Be(1);
        (await read.Set<InventoryLocation>().CountAsync()).Should().Be(1);
        var expected = sameKey ? request.Quantity : request.Quantity * 2;
        (await read.Set<StockBalance>().SingleAsync()).OnHandQuantity.Should().Be(expected);
        (await read.Set<Product>().SingleAsync()).StockQuantity.Should().Be(expected);
        (await read.Set<InventoryMovement>().CountAsync()).Should().Be(sameKey ? 1 : 2);
        var setup = await read.Set<InventarioSetup>().SingleAsync();
        setup.WarehouseId.Should().NotBeNull(); setup.LocationId.Should().NotBeNull();
        setup.CompletedAt.Should().BeNull("receiving must not complete setup or change preferences");
        setup.LowStockThreshold.Should().Be(9);
        (await read.Set<TenantModuleFeature>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Post_SameKeyAcrossTenants_ProducesIndependentReceipts()
    {
        var firstTenant = Guid.NewGuid(); var secondTenant = Guid.NewGuid();
        var firstRequest = await Seed(firstTenant); var secondRequest = await Seed(secondTenant);
        secondRequest = secondRequest with { IdempotencyKey = firstRequest.IdempotencyKey };
        await using var first = Database(firstTenant);
        await using var second = Database(secondTenant);
        (await Service(first, firstTenant).PostAsync(firstRequest)).IsSuccess.Should().BeTrue();
        (await Service(second, secondTenant).PostAsync(secondRequest)).IsSuccess.Should().BeTrue();
        (await first.Set<InventoryMovement>().SingleAsync()).ProductId.Should().Be(firstRequest.ProductId);
        (await second.Set<InventoryMovement>().SingleAsync()).ProductId.Should().Be(secondRequest.ProductId);
        var foreign = await Service(second, secondTenant).PostAsync(secondRequest with
        {
            ProductId = firstRequest.ProductId, IdempotencyKey = Guid.NewGuid().ToString()
        });
        foreign.StatusCode.Should().Be(404);
        (await second.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Post_ReplayAfterFeatureRevocation_StillRequiresFeature()
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        await using var first = Database(tenant);
        (await Service(first, tenant).PostAsync(request)).IsSuccess.Should().BeTrue();
        await using var retry = Database(tenant);
        (await Service(retry, tenant, gates: new Gates("movements")).PostAsync(request)).StatusCode.Should().Be(403);
        (await retry.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Post_RetryAfterDefaultChange_UsesOriginalDimensionsAndRejectsChangedPayload()
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        await using var first = Database(tenant);
        var original = await Service(first, tenant).PostAsync(request);
        original.IsSuccess.Should().BeTrue(original.Message);
        await using (var change = Database(tenant))
        {
            var (warehouse, location) = Storage(tenant);
            change.AddRange(warehouse, location);
            var setup = await change.Set<InventarioSetup>().AsTracking().SingleAsync();
            setup.WarehouseId = warehouse.Id; setup.LocationId = location.Id;
            await change.SaveChangesAsync();
        }
        await using var retry = Database(tenant);
        var replay = await Service(retry, tenant, manager: false, createStorage: false).PostAsync(request);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Data!.IsIdempotentReplay.Should().BeTrue();
        replay.Data.WarehouseId.Should().Be(original.Data!.WarehouseId);
        foreach (var changed in new[] { request with { Quantity = 8 }, request with { Reason = "different" },
                     request with { ProductVariationId = Guid.NewGuid() }, request with { ProductId = Guid.NewGuid() } })
            (await Service(retry, tenant).PostAsync(changed)).StatusCode.Should().Be(409);
        (await retry.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Post_ExistingAdvancedStorage_ReusesWithoutRenamingOrChangingFlags(bool defaults)
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        var (warehouse, location) = Storage(tenant);
        await using (var seed = Database(tenant))
        {
            seed.AddRange(warehouse, location);
            if (defaults)
            {
                var setup = await seed.Set<InventarioSetup>().AsTracking().SingleAsync();
                setup.WarehouseId = warehouse.Id; setup.LocationId = location.Id;
            }
            await seed.SaveChangesAsync();
        }
        await using var db = Database(tenant);
        var result = await Service(db, tenant, manager: !defaults, createStorage: false).PostAsync(request);
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.WarehouseId.Should().Be(warehouse.Id); result.Data.LocationId.Should().Be(location.Id);
        (await db.Set<Warehouse>().SingleAsync()).ConcurrencyStamp.Should().Be(warehouse.ConcurrencyStamp);
        (await db.Set<InventoryLocation>().SingleAsync()).ConcurrencyStamp.Should().Be(location.ConcurrencyStamp);
        (await db.Set<Warehouse>().SingleAsync()).Name.Should().Be("Existing advanced warehouse");
    }

    [TestCase("missing", 404)]
    [TestCase("variant", 400)]
    [TestCase("foreign", 404)]
    [TestCase("overflow", 409)]
    public async Task Post_FailureAfterProvisioning_RollsBackDefaultsStorageLedgerAndSnapshot(string failure, int status)
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        var productId = request.ProductId;
        if (failure == "missing") request = request with { ProductId = Guid.NewGuid() };
        if (failure is "variant" or "foreign")
        {
            var otherTenant = failure == "foreign" ? Guid.NewGuid() : tenant;
            var other = await Seed(otherTenant, setup: false);
            await using var seed = Database(otherTenant);
            var variant = new ProductVariation { Id = Guid.NewGuid(), TenantId = otherTenant, ProductId = other.ProductId, Name = "Other", IsEnabled = true };
            seed.Add(variant); await seed.SaveChangesAsync();
            request = request with { ProductVariationId = variant.Id };
        }
        if (failure == "overflow")
        {
            await using var seed = Database(tenant);
            var product = await seed.Set<Product>().AsTracking().SingleAsync();
            product.StockQuantity = 99999999999999.9999m;
            await seed.SaveChangesAsync();
        }
        await using var db = Database(tenant);
        var result = await Service(db, tenant).PostAsync(request);
        result.StatusCode.Should().Be(status, result.Message);
        await db.SaveChangesAsync(); // A failed receipt must not leave staged writes that can escape rollback.
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await db.Set<InventoryLocation>().CountAsync()).Should().Be(0);
        (await db.Set<StockBalance>().CountAsync()).Should().Be(0);
        (await db.Set<InventoryMovement>().CountAsync()).Should().Be(0);
        (await db.Set<InventarioSetup>().SingleAsync()).WarehouseId.Should().BeNull();
        (await db.Set<Product>().SingleAsync(x => x.Id == productId))
            .StockQuantity.Should().Be(failure == "overflow" ? 99999999999999.9999m : 0);
    }

    [TestCase(null)]
    [TestCase("stock_balances")]
    [TestCase("movements")]
    [TestCase("warehousing")]
    public async Task Post_DisabledFeature_RejectsBeforeProvisioning(string? disabled)
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        await using var db = Database(tenant);
        var result = await Service(db, tenant, gates: new Gates(disabled ?? "root")).PostAsync(request);
        result.StatusCode.Should().Be(403);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await db.Set<InventoryMovement>().CountAsync()).Should().Be(0);
    }

    [TestCase("actor", 401)]
    [TestCase("expired", 401)]
    [TestCase("tenant", 403)]
    [TestCase("capability", 403)]
    [TestCase("manager", 403)]
    [TestCase("storage", 403)]
    [TestCase("advanced", 409)]
    [TestCase("unconfigured", 409)]
    public async Task Post_InvalidAccessOrMode_RejectsWithoutProvisioning(string reason, int status)
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant, setup: reason != "unconfigured");
        await using var db = Database(tenant);
        if (reason == "tenant") request.Metadata.RequestedTenantId = Guid.NewGuid();
        if (reason == "advanced")
        {
            var setup = await db.Set<InventarioSetup>().AsTracking().SingleAsync();
            setup.Mode = InventarioSetupMode.Advanced; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        var result = await Service(db, tenant, actor: reason != "actor", expired: reason == "expired",
            movement: reason != "capability", manager: reason != "manager", createStorage: reason != "storage").PostAsync(request);
        result.StatusCode.Should().Be(status, result.Message);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await db.Set<InventoryMovement>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Post_Variant_UsesMatchingLedgerAndKeepsBaseStockSeparate()
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        var variant = new ProductVariation { Id = Guid.NewGuid(), TenantId = tenant, ProductId = request.ProductId, Name = "Small", IsEnabled = true };
        await using (var seed = Database(tenant)) { seed.Add(variant); await seed.SaveChangesAsync(); }
        await using var db = Database(tenant);
        var result = await Service(db, tenant).PostAsync(request with { ProductVariationId = variant.Id });
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.ProductVariationId.Should().Be(variant.Id);
        (await db.Set<StockBalance>().SingleAsync()).ProductVariationId.Should().Be(variant.Id);
        (await db.Set<InventoryMovement>().SingleAsync()).MovementType.Should().Be(InventoryMovementType.Receipt);
        (await db.Set<Product>().SingleAsync()).StockQuantity.Should().Be(request.Quantity);
    }

    [TestCase("foreign")]
    [TestCase("disabled")]
    [TestCase("nonpickable")]
    [TestCase("mismatch")]
    public async Task Post_InvalidExplicitDefaults_DoesNotSilentlyReplaceStorage(string reason)
    {
        var tenant = Guid.NewGuid(); var request = await Seed(tenant);
        var storageTenant = reason == "foreign" ? Guid.NewGuid() : tenant;
        var (warehouse, location) = Storage(storageTenant);
        warehouse.IsEnabled = reason != "disabled"; location.IsPickable = reason != "nonpickable";
        await using (var seed = Database(storageTenant)) { seed.AddRange(warehouse, location); await seed.SaveChangesAsync(); }
        await using (var seed = Database(tenant))
        {
            var setup = await seed.Set<InventarioSetup>().AsTracking().SingleAsync();
            setup.WarehouseId = reason == "mismatch" ? Guid.NewGuid() : warehouse.Id; setup.LocationId = location.Id;
            await seed.SaveChangesAsync();
        }
        await using var db = Database(tenant);
        (await Service(db, tenant).PostAsync(request)).StatusCode.Should().Be(409);
        (await db.Set<StockBalance>().CountAsync()).Should().Be(0);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(reason == "foreign" ? 0 : 1);
    }

    private async Task<PostBasicProductStockRequest> Seed(Guid tenant, bool setup = true)
    {
        await using var db = Database(tenant);
        var category = new ProductCategory { Id = Guid.NewGuid(), TenantId = tenant, Name = "Test", IsEnabled = true };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenant, Name = "Test", CategoryId = category.Id, IsEnabled = true };
        db.AddRange(category, product);
        if (setup) db.Add(new InventarioSetup { Id = Guid.NewGuid(), TenantId = tenant, Mode = InventarioSetupMode.Basic, LowStockThreshold = 9, IsEnabled = true });
        await db.SaveChangesAsync();
        return new() { ProductId = product.Id, Quantity = 1.2345m, Reason = "Receive", IdempotencyKey = Guid.NewGuid().ToString() };
    }

    private static (Warehouse, InventoryLocation) Storage(Guid tenant)
    {
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = Guid.NewGuid().ToString(), Name = "Existing advanced warehouse", IsDefault = true, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() };
        return (warehouse, new InventoryLocation { Id = Guid.NewGuid(), TenantId = tenant, WarehouseId = warehouse.Id,
            Code = "BIN", Name = "Existing bin", IsEnabled = true, IsPickable = true, ConcurrencyStamp = Guid.NewGuid() });
    }

    private AppDbContext Database(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning, RelationalEventId.BoolWithDefaultWarning)).Options,
        new HttpContextAccessor(), new ConfigurationBuilder().Build(), new Invocation(tenant));

    private static BasicProductStockService Service(AppDbContext db, Guid tenant, bool actor = true, bool expired = false,
        bool movement = true, bool manager = true, bool createStorage = true, Gates? gates = null)
    {
        var invocation = new Invocation(tenant, actor, expired, movement, manager, createStorage);
        gates ??= new Gates();
        var data = new ServerDataContext<AppDbContext>(db);
        return new(db, invocation, gates, new StockPostingService(data, invocation, gates, new ProductVariationService(data, invocation, gates), db),
            NullLogger<BasicProductStockService>.Instance);
    }

    private sealed class Invocation(Guid tenant, bool actor = true, bool expired = false, bool movement = true, bool manager = true, bool createStorage = true) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext Current { get; } = new(actor ? new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string>(new[] { movement ? "inventario.movements:create" : "", manager ? "identity.tenants:manage" : "", createStorage ? "inventario.warehousing:create" : "" }),
            "test", DateTimeOffset.UtcNow.AddHours(expired ? -1 : 1)) : null, null, tenant, null, Guid.NewGuid());
    }

    private sealed class Gates(string? disabled = null) : ITenantModuleFeatureService
    {
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Task.FromResult((subFeatureKey ?? "root") == disabled ? Result.Forbidden("Feature disabled.") : Result.Success());
        public async Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Result<bool>.Success((await EnsureEnabledAsync(tenantId, moduleKey, subFeatureKey, ct)).IsSuccess);
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) { }
    }
}
