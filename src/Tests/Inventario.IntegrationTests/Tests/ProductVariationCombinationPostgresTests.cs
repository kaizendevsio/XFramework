using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

// Own PostgreSQL only; do not start the deployed-service Bolt fixture for these persistence checks.
namespace InventarioCombinations.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Variations")]
public sealed class ProductVariationCombinationPostgresTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder().WithDatabase("inventario_combination_tests")
            .WithUsername("test").WithPassword("test_password").Build();
        await _postgres.StartAsync();
        _ = typeof(RegistryConfiguration).Assembly;
        _ = typeof(ProductVariation).Assembly;
        await using var db = Database(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Test]
    public async Task CreateUpdate_JsonOptionsAndZeroPrice_PersistWithoutChangingSellableIdentity()
    {
        var tenant = Guid.NewGuid();
        var request = await Seed(tenant);
        await using var db = Database(tenant);
        var created = await Service(db, tenant).CreateVariationAsync(request);
        created.IsSuccess.Should().BeTrue(created.Message);
        var id = created.Data!.Id;
        db.ChangeTracker.Clear();
        var saved = await db.Set<ProductVariation>().SingleAsync(x => x.Id == id);
        saved.CombinationOptions.Should().Equal(ProductVariationCombination.Normalize(request.CombinationOptions!));
        saved.CombinationKey.Should().Be(ProductVariationCombination.CreateKey(request.CombinationOptions!));
        saved.ProductVariationTypeId.Should().BeNull();
        var update = new UpdateProductVariationRequest
        {
            ProductVariationId = id, Name = "Complimentary red large", Price = 0,
            CombinationOptions = saved.CombinationOptions, ExpectedConcurrencyStamp = saved.ConcurrencyStamp
        };
        var updated = await Service(db, tenant).UpdateVariationAsync(update);
        updated.IsSuccess.Should().BeTrue(updated.Message);
        updated.Data!.Id.Should().Be(id);
        db.ChangeTracker.Clear();
        (await Service(db, tenant).UpdateVariationAsync(update)).StatusCode.Should().Be(409);
        var product = await db.Set<Product>().AsTracking().SingleAsync(x => x.Id == request.ProductId);
        product.Price = 99;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var catalog = new ProductService(new ServerDataContext<AppDbContext>(db), db, Mock.Of<ICacheService>(),
            NullLogger<ProductService>.Instance, new Invocation(tenant));
        var sellable = await catalog.GetSellableProductAsync(new GetSellableProductRequest { ProductId = request.ProductId });
        sellable.IsSuccess.Should().BeTrue(sellable.Message);
        sellable.Data!.Variations.Should().ContainSingle(x => x.ProductVariationId == id && x.Price == 0m);
        saved = await db.Set<ProductVariation>().SingleAsync(x => x.Id == id);
        saved.GetSellingPrice(99).Should().Be(0m);
        (await Service(db, tenant).UpdateVariationAsync(new()
        {
            ProductVariationId = id, ProductVariationTypeId = request.CombinationOptions![0].ProductVariationTypeId,
            Name = "Legacy client", Price = 5
        })).StatusCode.Should().Be(400, "an old client must not silently erase the structured combination");
    }

    [Test]
    public async Task Create_NormalizedDuplicate_ReturnsConflictAndLegacyRowsRemainNull()
    {
        var tenant = Guid.NewGuid();
        var request = await Seed(tenant);
        await using var db = Database(tenant);
        (await Service(db, tenant).CreateVariationAsync(request)).IsSuccess.Should().BeTrue();
        db.ChangeTracker.Clear();
        var duplicate = await Service(db, tenant).CreateVariationAsync(request with
        {
            Name = "Different label", Price = 20,
            CombinationOptions = request.CombinationOptions!.AsEnumerable().Reverse().Select(x => x with { Value = "  " + x.Value.ToUpperInvariant() + "  " }).ToList()
        });
        duplicate.StatusCode.Should().Be(409);
        var legacy = await Service(db, tenant).CreateVariationAsync(request with
        {
            Name = "Single option", CombinationOptions = null,
            ProductVariationTypeId = request.CombinationOptions![0].ProductVariationTypeId
        });
        legacy.IsSuccess.Should().BeTrue(legacy.Message);
        db.ChangeTracker.Clear();
        var saved = await db.Set<ProductVariation>().SingleAsync(x => x.Id == legacy.Data!.Id);
        saved.CombinationOptions.Should().BeNull();
        saved.CombinationKey.Should().BeNull();
    }

    [TestCase("foreign-tenant")]
    [TestCase("foreign-product")]
    [TestCase("disabled")]
    [TestCase("deleted")]
    public async Task Create_UnavailableOptionType_RejectsWithoutSaving(string reason)
    {
        var tenant = Guid.NewGuid();
        var request = await Seed(tenant);
        if (reason == "foreign-tenant")
        {
            var foreign = await Seed(Guid.NewGuid());
            request = request with
            {
                CombinationOptions = [foreign.CombinationOptions![0], request.CombinationOptions![1]]
            };
        }
        await using var db = Database(tenant);
        var type = reason == "foreign-tenant" ? null : await db.Set<ProductVariationType>().AsTracking()
            .SingleAsync(x => x.Id == request.CombinationOptions![0].ProductVariationTypeId);
        if (reason == "foreign-product")
        {
            var other = await Seed(tenant);
            type!.ProductId = other.ProductId;
        }
        if (reason == "disabled") type!.IsEnabled = false;
        if (reason == "deleted") type!.IsDeleted = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await Service(db, tenant).CreateVariationAsync(request);
        result.StatusCode.Should().Be(400);
        (await db.Set<ProductVariation>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task UniqueIndex_ConcurrentInserts_OnlyOneWinsAndSoftDeleteAllowsReplacement()
    {
        var tenant = Guid.NewGuid();
        var request = await Seed(tenant);
        var outcomes = await Task.WhenAll(Insert(tenant, request), Insert(tenant, request));
        outcomes.Should().ContainSingle(x => x);
        await using var db = Database(tenant);
        var saved = await db.Set<ProductVariation>().AsTracking().SingleAsync();
        saved.IsDeleted = true;
        await db.SaveChangesAsync();
        (await Insert(tenant, request)).Should().BeTrue();

        // The same selection key is allowed for another product; tenant/product is part of identity.
        var other = await Seed(tenant);
        (await Insert(tenant, other with { CombinationOptions = request.CombinationOptions })).Should().BeTrue();
    }

    [Test]
    public async Task Mapping_CombinationColumnsAndIndex_MatchScopedSnapshot()
    {
        await using var db = Database(Guid.NewGuid());
        var actual = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProductVariation))!;
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.FindEntityType(typeof(ProductVariation).FullName!)!;
        foreach (var name in new[] { nameof(ProductVariation.CombinationOptions), nameof(ProductVariation.CombinationKey) })
        {
            var property = actual.FindProperty(name)!;
            var stored = snapshot.FindProperty(name)!;
            stored.GetColumnType().Should().Be(property.GetColumnType());
            stored.IsNullable.Should().Be(property.IsNullable);
            stored.GetMaxLength().Should().Be(property.GetMaxLength());
        }
        var index = actual.GetIndexes().Single(x => x.GetDatabaseName() == "UX_ProductVariation_Tenant_Product_Combination");
        var snapshotIndex = snapshot.GetIndexes().Single(x => x.GetDatabaseName() == index.GetDatabaseName());
        index.IsUnique.Should().BeTrue();
        snapshotIndex.GetFilter().Should().Be(index.GetFilter());
        snapshotIndex.Properties.Select(x => x.Name).Should().Equal(index.Properties.Select(x => x.Name));
    }

    private async Task<bool> Insert(Guid tenant, CreateProductVariationRequest request)
    {
        await using var db = Database(tenant);
        db.Add(new ProductVariation
        {
            Id = Guid.NewGuid(), TenantId = tenant, ProductId = request.ProductId, Name = "Concurrent combination",
            Price = request.Price, CombinationOptions = ProductVariationCombination.Normalize(request.CombinationOptions!),
            CombinationKey = ProductVariationCombination.CreateKey(request.CombinationOptions!), IsEnabled = true
        });
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_ProductVariation_Tenant_Product_Combination" })
        { return false; }
    }

    private async Task<CreateProductVariationRequest> Seed(Guid tenant)
    {
        await using var db = Database(tenant);
        var category = new ProductCategory { Id = Guid.NewGuid(), TenantId = tenant, Name = Guid.NewGuid().ToString(), IsEnabled = true };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenant, Name = "Shirt", CategoryId = category.Id, Price = 10, IsAvailable = true, IsEnabled = true };
        var color = new ProductVariationType { Id = Guid.NewGuid(), TenantId = tenant, Name = "Color", NormalizedName = Guid.NewGuid().ToString(), IsEnabled = true };
        var size = new ProductVariationType { Id = Guid.NewGuid(), TenantId = tenant, ProductId = product.Id, Name = "Size", NormalizedName = "SIZE", IsEnabled = true };
        db.AddRange(category, product, color, size);
        await db.SaveChangesAsync();
        return new()
        {
            ProductId = product.Id, Name = "Red large", Price = 12,
            CombinationOptions = [new() { ProductVariationTypeId = color.Id, Value = "Red" }, new() { ProductVariationTypeId = size.Id, Value = "Large" }]
        };
    }

    private AppDbContext Database(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning, RelationalEventId.BoolWithDefaultWarning)).Options,
        new HttpContextAccessor(), new ConfigurationBuilder().Build(), new Invocation(tenant));

    private static ProductVariationService Service(AppDbContext db, Guid tenant)
    {
        var gates = new Mock<ITenantModuleFeatureService>();
        gates.Setup(x => x.EnsureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new(new ServerDataContext<AppDbContext>(db), new Invocation(tenant), gates.Object);
    }

    private sealed class Invocation(Guid tenant) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext Current { get; } = new(new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(),
            new HashSet<string>(), new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1)), null, tenant, null, Guid.NewGuid());
    }
}
