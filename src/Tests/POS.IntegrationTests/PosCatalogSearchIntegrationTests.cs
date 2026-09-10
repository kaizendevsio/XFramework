using System.Net;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts.Requests;
using Testcontainers.PostgreSql;
using XFramework.Core.Services.Caching;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
[Category(TestCategories.Catalog)]
public sealed class PosCatalogSearchIntegrationTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid clothingId = Guid.NewGuid();
    private readonly Guid drinkwareId = Guid.NewGuid();
    private PostgreSqlContainer? postgres;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("XFramework_POS_Catalog_Test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();
            await postgres.StartAsync();
        }
        catch (ArgumentException exception) when (exception.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("POS catalog integration tests require a Testcontainers-compatible Docker endpoint.");
        }

        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Product).TypeHandle);
        options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres!.GetConnectionString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shirt = Product("Cotton Shirt", "SKU-001", clothingId, "Acme");
        db.AddRange(
            new ProductCategory { Id = clothingId, TenantId = tenantId, Name = "Clothing", IsEnabled = true },
            new ProductCategory { Id = drinkwareId, TenantId = tenantId, Name = "Drinkware", IsEnabled = true },
            shirt,
            Product("Coffee Mug", "MUG-002", drinkwareId, null),
            new ProductVariation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = shirt.Id,
                Name = "Blue",
                VariationType = "Color",
                Price = 15,
                AdditionalPrice = 3,
                IsEnabled = true
            });
        await db.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (postgres is not null)
            await postgres.DisposeAsync();
    }

    [TestCase(null, new[] { "Coffee Mug", "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("   ", new[] { "Coffee Mug", "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("shirt", new[] { "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("  ShIrT  ", new[] { "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("sku-001", new[] { "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("-001", new[] { "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("acme", new[] { "Cotton Shirt", "Cotton Shirt - Blue" })]
    [TestCase("bLuE", new[] { "Cotton Shirt - Blue" })]
    [TestCase("color", new[] { "Cotton Shirt - Blue" })]
    [TestCase("garbage-no-such-product", new string[0])]
    public async Task SearchAsync_TextFilters_ReturnOnlyMatchingRows(string? search, string[] expected)
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // Warm the same service with the unfiltered catalog before every search.
        var initial = await service.SearchAsync(new SearchPosCatalogRequest(), CancellationToken.None);
        initial.IsSuccess.Should().BeTrue(initial.Message);
        initial.Data.Should().HaveCount(3);
        var result = await service.SearchAsync(new SearchPosCatalogRequest { Search = search }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(200);
        result.Data!.Select(item => item.DisplayName).Should().Equal(expected);
    }

    [Test]
    public async Task SearchAsync_CategoryAndText_CombineFiltersForBaseAndVariantRows()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var clothing = await service.SearchAsync(new SearchPosCatalogRequest { CategoryId = clothingId }, CancellationToken.None);
        var variant = await service.SearchAsync(new SearchPosCatalogRequest { CategoryId = clothingId, Search = "blue" }, CancellationToken.None);
        var excluded = await service.SearchAsync(new SearchPosCatalogRequest { CategoryId = drinkwareId, Search = "shirt" }, CancellationToken.None);

        clothing.IsSuccess.Should().BeTrue(clothing.Message);
        clothing.Data!.Select(item => item.DisplayName).Should().Equal("Cotton Shirt", "Cotton Shirt - Blue");
        clothing.Data.Should().OnlyContain(item => item.CategoryId == clothingId && item.CategoryName == "Clothing");
        variant.IsSuccess.Should().BeTrue(variant.Message);
        variant.Data.Should().ContainSingle().Which.VariantName.Should().Be("Blue");
        excluded.IsSuccess.Should().BeTrue(excluded.Message);
        excluded.Data.Should().BeEmpty();
    }

    [TestCase(true, false, new[] { "Cotton Shirt" })]
    [TestCase(false, true, new[] { "Cotton Shirt - Blue" })]
    [TestCase(false, false, new string[0])]
    public async Task SearchAsync_InclusionFilters_RestrictMatchingRows(bool includeBase, bool includeVariants, string[] expected)
    {
        await using var db = CreateContext();
        var result = await CreateService(db).SearchAsync(new SearchPosCatalogRequest
        {
            Search = "shirt",
            IncludeBaseProducts = includeBase,
            IncludeVariants = includeVariants
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Select(item => item.DisplayName).Should().Equal(expected);
    }

    [Test]
    public async Task SearchAsync_PagedSearch_FiltersBeforePagination()
    {
        await using var db = CreateContext();
        var result = await CreateService(db).SearchAsync(new SearchPosCatalogRequest
        {
            Search = "shirt",
            Page = 2,
            PageSize = 1
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().ContainSingle().Which.DisplayName.Should().Be("Cotton Shirt - Blue");
    }

    private PosCatalogService CreateService(AppDbContext db)
    {
        var invocation = new TrustedInvocationContext(null, null, tenantId, null, Guid.NewGuid());
        var trustedContext = Mock.Of<ITrustedInvocationContextAccessor>(accessor => accessor.Current == invocation);
        var products = new ProductService(
            Mock.Of<IDataContext>(), db, new Mock<ICacheService>(MockBehavior.Strict).Object,
            NullLogger<ProductService>.Instance, trustedContext);
        var inventario = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        // Substitute only transport; execute the owning service's real PostgreSQL query.
        inventario.Setup(item => item.SearchSellableProducts(
                It.IsAny<SearchSellableProductsRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (SearchSellableProductsRequest request, CancellationToken ct) =>
            {
                var result = await products.SearchSellableProductsAsync(request, ct);
                return new QueryResponse<List<SellableProductCatalogItem>>
                {
                    HttpStatusCode = (HttpStatusCode)result.StatusCode,
                    Message = result.Message,
                    Response = result.Data
                };
            });
        return new PosCatalogService(inventario.Object, db, new PosRequestContextResolver(trustedContext));
    }

    private AppDbContext CreateContext() => new(
        options, new HttpContextAccessor(), new ConfigurationBuilder().Build(),
        new TestEffectiveTenantContextAccessor(tenantId));

    private Product Product(string name, string sku, Guid categoryId, string? brand) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = name, SKU = sku,
        CategoryId = categoryId, Brand = brand, IsAvailable = true, IsEnabled = true, Price = 12
    };
}
