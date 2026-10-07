using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Enums;

// This fixture needs only an owned PostgreSQL container, not the assembly's deployed-service-style Bolt host.
namespace InventarioOnboarding.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed class InventarioSetupPostgresTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder().WithDatabase("inventario_setup_tests").WithUsername("test").WithPassword("test_password").Build();
        await _postgres.StartAsync();
        _ = typeof(RegistryConfiguration).Assembly;
        _ = typeof(Warehouse).Assembly;
        await using var db = Database(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (_postgres is not null) await _postgres.DisposeAsync(); }

    [Test]
    public async Task SetupSnapshot_MatchesScopedEntityMapping()
    {
        await using var db = Database(Guid.NewGuid());
        var actual = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InventarioSetup))!;
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.FindEntityType(typeof(InventarioSetup).FullName!)!;
        snapshot.Should().NotBeNull("the next migration must not recreate the setup table");
        var table = StoreObjectIdentifier.Table("InventarioSetup", "Inventario");
        snapshot.GetProperties().Select(p => new { p.Name, Column = p.GetColumnName(table), Type = p.GetColumnType(), p.IsNullable, p.IsConcurrencyToken, Max = p.GetMaxLength(), DefaultSql = p.GetDefaultValueSql() })
            .Should().BeEquivalentTo(actual.GetProperties().Select(p => new { p.Name, Column = p.GetColumnName(table), Type = p.GetColumnType(), p.IsNullable, p.IsConcurrencyToken, Max = p.GetMaxLength(), DefaultSql = p.GetDefaultValueSql() }));
        snapshot.GetIndexes().Select(i => new { Properties = i.Properties.Select(p => p.Name), i.IsUnique })
            .Should().BeEquivalentTo(actual.GetIndexes().Select(i => new { Properties = i.Properties.Select(p => p.Name), i.IsUnique }));
    }

    [Test]
    public async Task Get_FreshTenant_DoesNotWriteUntilConfirmed()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var result = await Service(db, tenant).GetAsync(new());
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.ShouldPrompt.Should().BeTrue();
        result.Data.Mode.Should().BeNull();
        (await db.Set<InventarioSetup>().CountAsync()).Should().Be(0);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Get_OrdinaryUser_DoesNotPromptForManagementSetupOrWriteDefaults()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var result = await Service(db, tenant, manager: false).GetAsync(new());
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.CanManage.Should().BeFalse();
        result.Data.ShouldPrompt.Should().BeFalse();
        (await db.Set<InventarioSetup>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Complete_BasicAndReplay_PersistsOnlyMinimumStorageAndDefaults()
    {
        var tenant = Guid.NewGuid();
        var request = Basic();
        await using var db = Database(tenant);
        var first = await Service(db, tenant).CompleteAsync(request);
        first.IsSuccess.Should().BeTrue(first.Message);
        await using var reload = Database(tenant);
        var replay = await Service(reload, tenant).CompleteAsync(request with { CompletionRequestId = Guid.NewGuid() });
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Data!.WarehouseId.Should().Be(first.Data!.WarehouseId);
        replay.Data.CompletedAt.Should().NotBeNull();
        replay.Data.Mode.Should().Be(InventarioSetupMode.Basic);
        replay.Data.LowStockThreshold.Should().Be(9);
        replay.Data.DefaultCurrency.Should().Be("SGD");
        (await reload.Set<Warehouse>().CountAsync()).Should().Be(1);
        (await reload.Set<InventoryLocation>().CountAsync()).Should().Be(1);
        (await reload.Set<InventarioSetup>().CountAsync()).Should().Be(1);
        (await reload.Set<Product>().CountAsync()).Should().Be(0);
        (await reload.Set<StockBalance>().CountAsync()).Should().Be(0);
        (await reload.Set<InventoryMovement>().CountAsync()).Should().Be(0);
        (await reload.Set<RegistryConfiguration>().CountAsync()).Should().Be(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Complete_ConcurrentConfirmations_AreSerializedAndReplaySafe(bool differentDraft)
    {
        var tenant = Guid.NewGuid();
        var request = Basic();
        await using var first = Database(tenant);
        await using var second = Database(tenant);
        var other = differentDraft ? request with { LowStockThreshold = 20 } : request;
        var results = await Task.WhenAll(Service(first, tenant).CompleteAsync(request), Service(second, tenant).CompleteAsync(other));
        results.Count(x => x.IsSuccess).Should().Be(differentDraft ? 1 : 2);
        if (differentDraft) results.Single(x => !x.IsSuccess).StatusCode.Should().Be(409);
        await using var read = Database(tenant);
        (await read.Set<Warehouse>().CountAsync()).Should().Be(1);
        (await read.Set<InventoryLocation>().CountAsync()).Should().Be(1);
        (await read.Set<InventarioSetup>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Complete_InvalidParent_DoesNotPartiallyCreateWarehouse()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var result = await Service(db, tenant).CompleteAsync(Basic() with { Location = new() { Code = "MAIN", Name = "Main", ParentLocationId = Guid.NewGuid() } });
        result.StatusCode.Should().Be(404);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await db.Set<InventoryLocation>().CountAsync()).Should().Be(0);
        (await db.Set<InventarioSetup>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Complete_DatabaseConstraintFailure_RollsBackAllNewRows()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        db.Add(new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "MAIN", Name = "Deleted warehouse", IsDeleted = true, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var result = await Service(db, tenant).CompleteAsync(Basic());
        result.IsSuccess.Should().BeFalse(); result.StatusCode.Should().Be(409);
        await using var read = Database(tenant);
        (await read.Set<InventarioSetup>().CountAsync()).Should().Be(0);
        (await read.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await read.Set<InventoryLocation>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Complete_AdvancedExistingStorage_DoesNotOverwriteExistingWarehouseOrLocation()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "KEEP", Name = "Existing", IsDefault = true, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() };
        var location = new InventoryLocation { Id = Guid.NewGuid(), TenantId = tenant, WarehouseId = warehouse.Id, Code = "KEEP", Name = "Existing bin", IsEnabled = true, IsPickable = false, ConcurrencyStamp = Guid.NewGuid() };
        db.AddRange(warehouse, location);
        await db.SaveChangesAsync();
        var service = Service(db, tenant);
        var read = await service.GetAsync(new());
        read.Data!.ShouldPrompt.Should().BeFalse();
        (await service.CompleteAsync(Basic())).StatusCode.Should().Be(409);
        var result = await service.CompleteAsync(Basic() with { Mode = InventarioSetupMode.Advanced, ExistingWarehouseId = warehouse.Id, ExistingLocationId = location.Id, Warehouse = null, Location = null });
        result.IsSuccess.Should().BeTrue(result.Message);
        await using var reload = Database(tenant);
        var savedWarehouse = await reload.Set<Warehouse>().SingleAsync();
        savedWarehouse.Name.Should().Be("Existing");
        savedWarehouse.IsDefault.Should().BeTrue();
        savedWarehouse.ConcurrencyStamp.Should().Be(warehouse.ConcurrencyStamp);
        (await reload.Set<InventoryLocation>().SingleAsync()).IsPickable.Should().BeFalse();
    }

    [Test]
    public async Task Complete_AdvancedFullOptions_PreservesAllExplicitStorageChoices()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var result = await Service(db, tenant).CompleteAsync(Basic() with
        {
            Mode = InventarioSetupMode.Advanced,
            Warehouse = new() { Code = "SG", Name = "Storage", Description = "Storage details", AddressLine = "QA address", City = "Singapore", Region = "Central", PostalCode = "123456", CountryCode = "SG", IsDefault = true },
            Location = new() { Code = "RECEIVE", Name = "Receiving", Description = "Inbound", LocationType = InventoryLocationType.Receiving, IsPickable = false }
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        await using var read = Database(tenant);
        var warehouse = await read.Set<Warehouse>().SingleAsync();
        warehouse.AddressLine.Should().Be("QA address"); warehouse.CountryCode.Should().Be("SG"); warehouse.PostalCode.Should().Be("123456");
        var location = await read.Set<InventoryLocation>().SingleAsync();
        location.LocationType.Should().Be(InventoryLocationType.Receiving); location.IsPickable.Should().BeFalse();
    }

    [Test]
    public async Task Complete_DisabledWarehousing_AdvancedDoesNotEnableFeaturesOrCreateStorage()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var service = Service(db, tenant, warehouseEnabled: false);
        (await service.CompleteAsync(Basic())).StatusCode.Should().Be(403);
        var result = await service.CompleteAsync(Basic() with { Mode = InventarioSetupMode.Advanced, Warehouse = null, Location = null });
        result.IsSuccess.Should().BeTrue(result.Message);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
        (await db.Set<TenantModuleFeature>().CountAsync()).Should().Be(0);
    }

    [TestCase("actor", 401)]
    [TestCase("permission", 403)]
    [TestCase("tenant", 403)]
    [TestCase("feature", 403)]
    public async Task Complete_UnauthorizedActorTenantOrFeature_IsRejectedWithoutWrites(string reason, int status)
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var request = Basic();
        if (reason == "tenant") request.Metadata.RequestedTenantId = Guid.NewGuid();
        var result = await Service(db, tenant, actor: reason != "actor", manager: reason != "permission", enabled: reason != "feature").CompleteAsync(request);
        result.IsSuccess.Should().BeFalse(); result.StatusCode.Should().Be(status);
        (await db.Set<InventarioSetup>().CountAsync()).Should().Be(0);
        (await db.Set<Warehouse>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Complete_ForeignWarehouse_IsNotFoundAndDoesNotWriteOtherTenant()
    {
        var firstTenant = Guid.NewGuid(); var secondTenant = Guid.NewGuid();
        await using var first = Database(firstTenant);
        var created = await Service(first, firstTenant).CompleteAsync(Basic());
        await using var second = Database(secondTenant);
        var result = await Service(second, secondTenant).CompleteAsync(Basic() with { Mode = InventarioSetupMode.Advanced, ExistingWarehouseId = created.Data!.WarehouseId, Warehouse = null });
        result.StatusCode.Should().Be(404);
        (await second.Set<InventarioSetup>().CountAsync()).Should().Be(0);
        (await second.Set<Warehouse>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Preferences_ConcurrentStampAndTenantIsolation_AreEnforced()
    {
        var tenant = Guid.NewGuid();
        await using var first = Database(tenant);
        var original = await Service(first, tenant).CompleteAsync(Basic());
        var request = new UpdateInventarioPreferencesRequest { ExpectedConcurrencyStamp = original.Data!.ConcurrencyStamp, LowStockThreshold = 12, DefaultCurrency = "USD" };
        await using var second = Database(tenant);
        (await Service(second, tenant).UpdatePreferencesAsync(request)).IsSuccess.Should().BeTrue();
        await using var stale = Database(tenant);
        (await Service(stale, tenant).UpdatePreferencesAsync(request)).StatusCode.Should().Be(409);
        await using var otherTenant = Database(Guid.NewGuid());
        (await otherTenant.Set<InventarioSetup>().CountAsync()).Should().Be(0);
    }

    private AppDbContext Database(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning, RelationalEventId.BoolWithDefaultWarning)).Options,
        new HttpContextAccessor(), new ConfigurationBuilder().Build(), new SetupInvocation(tenant));
    private static InventarioSetupService Service(DbContext db, Guid tenant, bool actor = true, bool manager = true, bool enabled = true, bool warehouseEnabled = true) =>
        new(db, new SetupInvocation(tenant, actor, manager), new SetupGates(enabled, warehouseEnabled), NullLogger<InventarioSetupService>.Instance);
    private static CompleteInventarioSetupRequest Basic() => new()
    {
        CompletionRequestId = Guid.NewGuid(), Mode = InventarioSetupMode.Basic, LowStockThreshold = 9, DefaultCurrency = "SGD",
        Warehouse = new CreateWarehouseRequest { Code = "MAIN", Name = "Main Warehouse", IsDefault = true },
        Location = new CreateInventoryLocationRequest { Code = "MAIN", Name = "Main Location", IsPickable = true }
    };

    private sealed class SetupInvocation(Guid tenant, bool actor = true, bool manager = true) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext Current { get; } = new(actor ? new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(),
            manager ? new HashSet<string> { XFrameworkActorCapabilities.IdentityTenantsManage } : new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1)) : null, null, tenant, null, Guid.NewGuid());
    }
    private sealed class SetupGates(bool enabled, bool warehousing) : ITenantModuleFeatureService
    {
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Task.FromResult(enabled && (subFeatureKey != TenantModuleFeatureKeys.WarehousingSubFeature || warehousing) ? Result.Success() : Result.Forbidden("Feature disabled."));
        public async Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Result<bool>.Success((await EnsureEnabledAsync(tenantId, moduleKey, subFeatureKey, ct)).IsSuccess);
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) { }
    }
}
