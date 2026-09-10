using System.Net;
using Inventario.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.POS)]
[Category(TestCategories.Catalog)]
public sealed class PosCatalogServiceTests
{
    private AppDbContext db = null!;
    private Mock<IInventarioServiceWrapper> inventario = null!;
    private PosCatalogService service = null!;

    [SetUp]
    public void SetUp()
    {
        var tenantId = Guid.NewGuid();
        db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().Options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(tenantId));
        inventario = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var resolver = new Mock<IPosRequestContextResolver>();
        resolver.Setup(item => item.Resolve(It.IsAny<RequestBase>(), null))
            .Returns((RequestBase request, Guid? _) => Result<PosRequestContext>.Success(
                new PosRequestContext(tenantId, Guid.NewGuid(), request.Metadata, false, false)));
        service = new PosCatalogService(inventario.Object, db, resolver.Object);
    }

    [TearDown]
    public void TearDown() => db.Dispose();

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ShIrT  ")]
    [TestCase("SKU-001")]
    [TestCase("Blue")]
    [TestCase("no-such-product")]
    public async Task SearchAsync_SerializedRequest_PreservesSearchAndFilters(string? search)
    {
        var request = new SearchPosCatalogRequest
        {
            Search = search,
            CategoryId = Guid.NewGuid(),
            IsAvailable = null,
            IncludeBaseProducts = false,
            IncludeVariants = true,
            Page = 2,
            PageSize = 7,
            Metadata = new RequestMetadata { RequestedTenantId = Guid.NewGuid() }
        };
        SearchSellableProductsRequest? forwarded = null;
        inventario.Setup(item => item.SearchSellableProducts(
                It.IsAny<SearchSellableProductsRequest>(), It.IsAny<CancellationToken>()))
            .Callback((SearchSellableProductsRequest value, CancellationToken _) =>
                forwarded = MemoryPackSerializer.Deserialize<SearchSellableProductsRequest>(
                    MemoryPackSerializer.Serialize(value)))
            .ReturnsAsync(Success([]));

        var wireRequest = MemoryPackSerializer.Deserialize<SearchPosCatalogRequest>(
            MemoryPackSerializer.Serialize(request))!;
        var result = await service.SearchAsync(wireRequest, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(200);
        result.Data.Should().BeEmpty();
        forwarded.Should().BeEquivalentTo(new SearchSellableProductsRequest
        {
            Search = search,
            CategoryId = request.CategoryId,
            IsAvailable = null,
            IncludeBaseProducts = false,
            IncludeVariants = true,
            Page = 2,
            PageSize = 7,
            Metadata = request.Metadata
        });
        inventario.VerifyAll();
        inventario.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SearchAsync_AllThenMatchThenNoMatch_DoesNotReusePreviousCatalog()
    {
        var shirt = Item("Shirt");
        var mug = Item("Mug");
        var variant = shirt with
        {
            ProductVariationId = Guid.NewGuid(),
            DisplayName = "Shirt - Blue",
            VariantName = "Blue"
        };
        inventario.Setup(item => item.SearchSellableProducts(
                It.Is<SearchSellableProductsRequest>(request => request.Search == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success([shirt, variant, mug]));
        inventario.Setup(item => item.SearchSellableProducts(
                It.Is<SearchSellableProductsRequest>(request => request.Search == "shirt"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success([shirt, variant]));
        inventario.Setup(item => item.SearchSellableProducts(
                It.Is<SearchSellableProductsRequest>(request => request.Search == "garbage"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success([]));

        var all = await service.SearchAsync(new SearchPosCatalogRequest(), CancellationToken.None);
        var matching = await service.SearchAsync(new SearchPosCatalogRequest { Search = "shirt" }, CancellationToken.None);
        var missing = await service.SearchAsync(new SearchPosCatalogRequest { Search = "garbage" }, CancellationToken.None);

        all.IsSuccess.Should().BeTrue(all.Message);
        all.Data.Should().HaveCount(3);
        matching.IsSuccess.Should().BeTrue(matching.Message);
        matching.Data.Should().BeEquivalentTo(new[] { shirt, variant }, options => options.ExcludingMissingMembers());
        missing.IsSuccess.Should().BeTrue(missing.Message);
        missing.StatusCode.Should().Be(200);
        missing.Data.Should().BeEmpty();
        inventario.VerifyAll();
        inventario.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SearchAsync_InventarioFailure_PreservesFailureInsteadOfReturningCatalog()
    {
        inventario.Setup(item => item.SearchSellableProducts(
                It.IsAny<SearchSellableProductsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<SellableProductCatalogItem>>
            {
                HttpStatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "Catalog unavailable"
            });

        var result = await service.SearchAsync(new SearchPosCatalogRequest { Search = "shirt" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Message.Should().Be("Catalog unavailable");
        result.Data.Should().BeNull();
    }

    [Test]
    public async Task SearchAsync_CancellableRequest_ForwardsTokenToCatalogWrapper()
    {
        using var cancellation = new CancellationTokenSource();
        inventario.Setup(item => item.SearchSellableProducts(
                It.IsAny<SearchSellableProductsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success([]));

        await service.SearchAsync(new SearchPosCatalogRequest(), cancellation.Token);

        inventario.Verify(item => item.SearchSellableProducts(
            It.IsAny<SearchSellableProductsRequest>(), cancellation.Token), Times.Once);
    }

    [Test]
    public async Task SearchAsync_StockScopedRequest_PreservesDimensionsAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var request = new SearchPosCatalogRequest
        {
            WarehouseId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Search = "Blue"
        };
        var variant = Item("Shirt") with { ProductVariationId = Guid.NewGuid(), VariantName = "Blue" };
        inventario.Setup(item => item.SearchSellableProducts(
                It.IsAny<SearchSellableProductsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success([variant]));
        inventario.Setup(item => item.GetStockBalances(
                It.IsAny<GetStockBalancesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<StockBalance>>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = [new StockBalance { AvailableQuantity = 3 }]
            });

        var result = await service.SearchAsync(request, cancellation.Token);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().ContainSingle().Which.AvailableQuantity.Should().Be(3);
        inventario.Verify(item => item.GetStockBalances(
            It.Is<GetStockBalancesRequest>(value =>
                value.ProductId == variant.ProductId &&
                value.ProductVariationId == variant.ProductVariationId &&
                value.WarehouseId == request.WarehouseId &&
                value.LocationId == request.LocationId &&
                value.Metadata == request.Metadata), cancellation.Token), Times.Once);
    }

    private static QueryResponse<List<SellableProductCatalogItem>> Success(List<SellableProductCatalogItem> items) => new()
    {
        HttpStatusCode = HttpStatusCode.OK,
        Response = items
    };

    private static SellableProductCatalogItem Item(string name) => new(
        Guid.NewGuid(), null, name, name, null, null, null,
        "SKU-001", "Acme", "product.png", Guid.NewGuid(), "Clothing", true, 12.5m);
}
