using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;

// Standalone database fixture; no deployed Bolt services or production data.
namespace InventarioBrands.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Brands")]
public sealed class ProductBrandPostgresTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        _postgres = new PostgreSqlBuilder().WithDatabase("inventario_brand_tests")
            .WithUsername("test").WithPassword("test_password").Build();
        await _postgres.StartAsync();
        _ = typeof(Product).Assembly;
        await using var db = Database(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    [Test]
    public async Task CreateAndUpdate_NormalizesBrandsWithoutChangingOtherProductsOrStock()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var legacy = await Product(db, tenant, category.Id, " Acme  tools ");
        var service = new ProductService(new ServerDataContext<AppDbContext>(db), Mock.Of<ICacheService>(),
            NullLogger<ProductService>.Instance, new Invocation(tenant));
        var created = await service.CreateAsync(new() { Name = "New", CategoryId = category.Id, Price = 20, Brand = "  aCMe\ttools " });
        created.IsSuccess.Should().BeTrue(created.Message);
        created.Data!.Brand.Should().Be("ACME TOOLS");
        db.ChangeTracker.Clear();
        var updated = await service.UpdateAsync(created.Data.Id, new()
        {
            Name = "Updated", CategoryId = category.Id, Price = 25, Brand = " oTher  tools "
        });
        updated.IsSuccess.Should().BeTrue(updated.Message);
        updated.Data!.Brand.Should().Be("OTHER TOOLS");
        updated.Data.StockQuantity.Should().Be(0);
        db.ChangeTracker.Clear();
        (await db.Set<Product>().SingleAsync(x => x.Id == legacy.Id)).Brand.Should().Be(" Acme  tools ");
        var blank = await service.UpdateAsync(created.Data.Id, new() { Name = "Updated", CategoryId = category.Id, Price = 25, Brand = " \t " });
        blank.IsSuccess.Should().BeTrue(blank.Message);
        blank.Data!.Brand.Should().BeNull();
        db.ChangeTracker.Clear();
        var invalid = await service.UpdateAsync(created.Data.Id, new() { Name = "Not saved", CategoryId = category.Id, Price = 1, Brand = new string('x', 101) });
        invalid.StatusCode.Should().Be(400);
        (await db.Set<Product>().SingleAsync(x => x.Id == created.Data.Id)).Name.Should().Be("Updated");
    }

    [Test]
    public async Task Search_NormalizesBeforeLimit_UsesOnlyCategoryAndTenantAndExcludesDeleted()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var other = await Category(db, tenant);
        foreach (var brand in new[] { " acme  tools ", "ACME TOOLS", "Acme\tTools", "Acme\u00a0\u2003Tools" })
            await Product(db, tenant, category.Id, brand);
        await Product(db, tenant, other.Id, "OTHER CATEGORY");
        await Product(db, tenant, category.Id, "DELETED", deleted: true);
        var foreignTenant = Guid.NewGuid();
        await using (var foreign = Database(foreignTenant))
        {
            var foreignCategory = await Category(foreign, foreignTenant);
            await Product(foreign, foreignTenant, foreignCategory.Id, "FOREIGN");
        }
        var result = await Service(db, tenant).SearchAsync(new() { CategoryId = category.Id });
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().Equal("ACME TOOLS");
        var filtered = await Service(db, tenant).SearchAsync(new() { CategoryId = category.Id, Search = " Acme\tTools " });
        filtered.Data.Should().Equal("ACME TOOLS");
        var missing = await Service(db, tenant).SearchAsync(new() { CategoryId = Guid.NewGuid() });
        missing.StatusCode.Should().Be(404);

        db.AddRange(Enumerable.Range(0, 60).Select(i => new Product
        {
            Id = Guid.NewGuid(), TenantId = tenant, CategoryId = category.Id, Name = $"Bounded {i}", Brand = $"BRAND {i:00}",
            IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
        }));
        await db.SaveChangesAsync();
        var bounded = await Service(db, tenant).SearchAsync(new() { CategoryId = category.Id, Limit = int.MaxValue });
        bounded.Data.Should().HaveCount(50).And.OnlyHaveUniqueItems();
        var small = await Service(db, tenant).SearchAsync(new() { CategoryId = category.Id, Limit = 1 });
        small.Data.Should().Equal("ACME TOOLS");
        var exact = await Service(db, tenant).SearchAsync(new() { CategoryId = category.Id, Search = "BRAND 59", Limit = 1 });
        exact.Data.Should().Equal("BRAND 59");
    }

    [Test]
    public async Task Merge_Confirmed_UpdatesOnlyMatchingCategoryBrandAndAuditFields()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var other = await Category(db, tenant);
        var source = await Product(db, tenant, category.Id, " acme  tools ");
        var target = await Product(db, tenant, category.Id, "TARGET");
        var unrelated = await Product(db, tenant, other.Id, " acme  tools ");
        var deleted = await Product(db, tenant, category.Id, "acme tools", deleted: true);
        var foreignTenant = Guid.NewGuid();
        Guid foreignId;
        await using (var foreign = Database(foreignTenant))
        {
            var foreignCategory = await Category(foreign, foreignTenant);
            foreignId = (await Product(foreign, foreignTenant, foreignCategory.Id, "acme tools")).Id;
        }
        var oldStamp = source.ConcurrencyStamp;
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);
        var preview = await service.PreviewAsync(new() { CategoryId = category.Id, SourceBrand = "ACME TOOLS", TargetBrand = "target" });
        preview.IsSuccess.Should().BeTrue(preview.Message);
        preview.Data!.AffectedProductCount.Should().Be(1);
        (await db.Set<Product>().SingleAsync(x => x.Id == source.Id)).Brand.Should().Be(" acme  tools ");

        // A brand merge must not accidentally save another workflow's tracked changes.
        var pending = await db.Set<Product>().AsTracking().SingleAsync(x => x.Id == target.Id);
        pending.Price = 999;
        var result = await service.MergeAsync(new()
        {
            CategoryId = category.Id, SourceBrand = " acme tools ", TargetBrand = " target ",
            Confirmed = true, ExpectedProductCount = preview.Data.AffectedProductCount
        });
        result.IsSuccess.Should().BeTrue(result.Message);
        await using var read = Database(tenant);
        var changed = await read.Set<Product>().SingleAsync(x => x.Id == source.Id);
        changed.Brand.Should().Be("TARGET");
        changed.ConcurrencyStamp.Should().NotBe(oldStamp);
        changed.ModifiedAt.Should().NotBeNull();
        changed.StockQuantity.Should().Be(source.StockQuantity);
        changed.Price.Should().Be(source.Price);
        changed.Name.Should().Be(source.Name);
        changed.SKU.Should().Be(source.SKU);
        changed.CategoryId.Should().Be(source.CategoryId);
        (await read.Set<Product>().SingleAsync(x => x.Id == unrelated.Id)).Brand.Should().Be(" acme  tools ");
        (await read.Set<Product>().SingleAsync(x => x.Id == target.Id)).Price.Should().Be(12);
        (await read.Set<Product>().IgnoreQueryFilters().SingleAsync(x => x.Id == deleted.Id)).Brand.Should().Be("acme tools");
        await using var foreignRead = Database(foreignTenant);
        (await foreignRead.Set<Product>().SingleAsync(x => x.Id == foreignId)).Brand.Should().Be("acme tools");
    }

    [TestCase("  acme   tools   co  ")]
    [TestCase("\t acme\u00a0 tools\u2003 co \r\n")]
    public async Task Normalization_MultipleWhitespaceRuns_SearchPreviewAndMergeAgree(string legacyBrand)
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var legacy = await Product(db, tenant, category.Id, legacyBrand);
        await Product(db, tenant, category.Id, "ACME TOOLS CO");
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);

        var search = await service.SearchAsync(new SearchProductBrandsRequest
        {
            CategoryId = category.Id, Search = " acme  tools  co ", Limit = 1
        });
        search.IsSuccess.Should().BeTrue(search.Message);
        search.Data.Should().Equal("ACME TOOLS CO");
        var preview = await service.PreviewAsync(new PreviewProductBrandMergeRequest
        {
            CategoryId = category.Id, SourceBrand = legacyBrand, TargetBrand = "ACME TOOLS CO"
        });
        preview.IsSuccess.Should().BeTrue(preview.Message);
        preview.Data!.AffectedProductCount.Should().Be(1);
        var merge = await service.MergeAsync(new MergeProductBrandsRequest
        {
            CategoryId = category.Id, SourceBrand = legacyBrand, TargetBrand = "ACME TOOLS CO",
            Confirmed = true, ExpectedProductCount = preview.Data.AffectedProductCount
        });
        merge.IsSuccess.Should().BeTrue(merge.Message);
        (await db.Set<Product>().SingleAsync(x => x.Id == legacy.Id)).Brand.Should().Be("ACME TOOLS CO");
        (await db.Set<Product>().Select(x => x.Brand).Distinct().ToListAsync()).Should().Equal("ACME TOOLS CO");
    }

    [Test]
    public async Task Merge_UnconfirmedOrStaleOrWrongCategory_DoesNotWrite()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var other = await Category(db, tenant);
        var source = await Product(db, tenant, category.Id, "SOURCE");
        await Product(db, tenant, category.Id, "TARGET");
        await Product(db, tenant, other.Id, "OTHER");
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);
        var request = new MergeProductBrandsRequest { CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET", ExpectedProductCount = 1 };
        (await service.MergeAsync(request)).StatusCode.Should().Be(400);
        (await service.MergeAsync(request with { Confirmed = true, ExpectedProductCount = 2 })).StatusCode.Should().Be(409);
        (await service.MergeAsync(request with { Confirmed = true, TargetBrand = "OTHER" })).StatusCode.Should().Be(404);
        (await service.MergeAsync(request with { Confirmed = true, SourceBrand = "MISSING" })).StatusCode.Should().Be(409);
        (await db.Set<Product>().SingleAsync(x => x.Id == source.Id)).Brand.Should().Be("SOURCE");
    }

    [Test]
    public async Task Merge_SameNormalizedBrand_CleansLegacyDuplicatesOnly()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        await Product(db, tenant, category.Id, " acme ");
        var canonical = await Product(db, tenant, category.Id, "ACME");
        var stamp = canonical.ConcurrencyStamp;
        db.ChangeTracker.Clear();
        var service = Service(db, tenant);
        var result = await service.MergeAsync(new() { CategoryId = category.Id, SourceBrand = "Acme", TargetBrand = "ACME", Confirmed = true, ExpectedProductCount = 1 });
        result.IsSuccess.Should().BeTrue(result.Message);
        (await db.Set<Product>().Select(x => x.Brand).Distinct().ToListAsync()).Should().Equal("ACME");
        (await db.Set<Product>().SingleAsync(x => x.Id == canonical.Id)).ConcurrencyStamp.Should().Be(stamp);
    }

    [Test]
    public async Task Access_MissingActorSpoofedTenantPermissionAndFeature_AreRejected()
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var search = new SearchProductBrandsRequest { CategoryId = category.Id };
        (await Service(db, tenant, actor: false).SearchAsync(search)).StatusCode.Should().Be(401);
        (await Service(db, tenant).SearchAsync(search with { Metadata = new() { RequestedTenantId = Guid.NewGuid() } })).StatusCode.Should().Be(403);
        (await Service(db, tenant, enabled: false).SearchAsync(search)).StatusCode.Should().Be(403);
        var merge = new MergeProductBrandsRequest { CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET", Confirmed = true, ExpectedProductCount = 1 };
        (await Service(db, tenant, manager: false).MergeAsync(merge)).StatusCode.Should().Be(403);
        (await Service(db, tenant, manager: false).PreviewAsync(new() { CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET" })).StatusCode.Should().Be(403);
        (await Service(db, tenant, manager: false).SearchAsync(search)).IsSuccess.Should().BeTrue();
    }

    [TestCase(true, false, true, 401)]
    [TestCase(true, true, true, 401)]
    [TestCase(false, true, false, 403)]
    public async Task Access_ExpiredOrUnprivilegedCrossTenantActor_RejectsEveryOperationWithoutWrites(
        bool expired, bool crossTenant, bool manager, int status)
    {
        var tenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var source = await Product(db, tenant, category.Id, "SOURCE");
        await Product(db, tenant, category.Id, "TARGET");
        var service = Service(db, tenant, manager: manager, expired: expired,
            actorTenant: crossTenant ? Guid.NewGuid() : tenant);

        (await service.SearchAsync(new SearchProductBrandsRequest { CategoryId = category.Id })).StatusCode.Should().Be(status);
        (await service.PreviewAsync(new PreviewProductBrandMergeRequest
        {
            CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET"
        })).StatusCode.Should().Be(status);
        (await service.MergeAsync(new MergeProductBrandsRequest
        {
            CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET",
            Confirmed = true, ExpectedProductCount = 1
        })).StatusCode.Should().Be(status);

        var unchanged = await db.Set<Product>().SingleAsync(x => x.Id == source.Id);
        unchanged.Brand.Should().Be("SOURCE");
        unchanged.ConcurrencyStamp.Should().Be(source.ConcurrencyStamp);
    }

    [Test]
    public async Task Access_AuthorizedCrossTenantManager_UsesEffectiveTenantForEveryOperation()
    {
        var tenant = Guid.NewGuid();
        var actorTenant = Guid.NewGuid();
        await using var db = Database(tenant);
        var category = await Category(db, tenant);
        var source = await Product(db, tenant, category.Id, "SOURCE");
        await Product(db, tenant, category.Id, "TARGET");
        await using var foreign = Database(actorTenant);
        var foreignCategory = await Category(foreign, actorTenant);
        var foreignSource = await Product(foreign, actorTenant, foreignCategory.Id, "SOURCE");
        var service = Service(db, tenant, actorTenant: actorTenant);

        var search = await service.SearchAsync(new SearchProductBrandsRequest { CategoryId = category.Id });
        search.IsSuccess.Should().BeTrue(search.Message);
        search.Data.Should().BeEquivalentTo(new[] { "SOURCE", "TARGET" });
        var preview = await service.PreviewAsync(new PreviewProductBrandMergeRequest
        {
            CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET"
        });
        preview.IsSuccess.Should().BeTrue(preview.Message);
        var merge = await service.MergeAsync(new MergeProductBrandsRequest
        {
            CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET",
            Confirmed = true, ExpectedProductCount = preview.Data!.AffectedProductCount
        });
        merge.IsSuccess.Should().BeTrue(merge.Message);
        (await db.Set<Product>().SingleAsync(x => x.Id == source.Id)).Brand.Should().Be("TARGET");
        (await foreign.Set<Product>().SingleAsync(x => x.Id == foreignSource.Id)).Brand.Should().Be("SOURCE");
        (await service.SearchAsync(new SearchProductBrandsRequest { CategoryId = foreignCategory.Id })).StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Merge_ConcurrentConfirmation_OnlyOneSucceeds()
    {
        var tenant = Guid.NewGuid();
        await using var seed = Database(tenant);
        var category = await Category(seed, tenant);
        await Product(seed, tenant, category.Id, "SOURCE");
        await Product(seed, tenant, category.Id, "TARGET");
        var request = new MergeProductBrandsRequest { CategoryId = category.Id, SourceBrand = "SOURCE", TargetBrand = "TARGET", Confirmed = true, ExpectedProductCount = 1 };
        await using var first = Database(tenant);
        await using var second = Database(tenant);
        var results = await Task.WhenAll(Service(first, tenant).MergeAsync(request), Service(second, tenant).MergeAsync(request));
        results.Count(x => x.IsSuccess).Should().Be(1);
        results.Single(x => !x.IsSuccess).StatusCode.Should().Be(409);
    }

    private AppDbContext Database(Guid tenant) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning, RelationalEventId.BoolWithDefaultWarning)).Options,
        new HttpContextAccessor(), new ConfigurationBuilder().Build(), new Invocation(tenant));

    private static ProductBrandService Service(AppDbContext db, Guid tenant, bool actor = true, bool manager = true,
        bool enabled = true, bool expired = false, Guid? actorTenant = null)
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(0));
        var gates = new Mock<ITenantModuleFeatureService>();
        gates.Setup(x => x.EnsureEnabledAsync(tenant, "inventario", "catalog", It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled ? Result.Success() : Result.Forbidden("Disabled"));
        return new(db, new Invocation(tenant, actor, manager, expired, actorTenant), gates.Object, cache.Object, NullLogger<ProductBrandService>.Instance);
    }

    private static async Task<ProductCategory> Category(AppDbContext db, Guid tenant)
    {
        var category = new ProductCategory { Id = Guid.NewGuid(), TenantId = tenant, Name = Guid.NewGuid().ToString(), IsEnabled = true, ConcurrencyStamp = Guid.NewGuid() };
        db.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> Product(AppDbContext db, Guid tenant, Guid category, string brand, bool deleted = false)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenant, CategoryId = category, Name = "Test product", Brand = brand,
            Price = 12, StockQuantity = 3, SKU = Guid.NewGuid().ToString(), IsEnabled = true, IsDeleted = deleted, ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private sealed class Invocation(Guid tenant, bool actor = true, bool manager = true,
        bool expired = false, Guid? actorTenant = null) : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext Current { get; } = new(actor ? new TrustedActorIdentity(Guid.NewGuid(), null, actorTenant ?? tenant, Guid.NewGuid(),
            new HashSet<string>(), manager ? new HashSet<string> { XFrameworkActorCapabilities.IdentityTenantsManage } : new HashSet<string>(),
            "test", DateTimeOffset.UtcNow.AddHours(expired ? -1 : 1)) : null, null, tenant, null, Guid.NewGuid());
    }
}

