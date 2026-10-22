using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.Playwright;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    private readonly Mock<global::IdentityServer.Integration.Drivers.IIdentityServerServiceWrapper> _stockFeatureIdentity = new(MockBehavior.Strict);

    [TestCase(true, false, false, true)]
    [TestCase(false, false, false, true)]
    [TestCase(true, true, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(false, false, true, true)]
    [TestCase(true, true, true, false)]
    [TestCase(false, true, true, false)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_DisabledStockFeatures_StockRemainsDiscoverableAndEnableRequiresAdmin(
        bool canManage, bool balancesEnabled, bool movementsEnabled, bool warehousingEnabled)
    {
        _state.CanManage = canManage;
        PrepareProductWorkflow();
        if (!balancesEnabled) _state.DisabledWorkflowFeatures.Add("stock_balances");
        if (!movementsEnabled) _state.DisabledWorkflowFeatures.Add("movements");
        if (!warehousingEnabled) _state.DisabledWorkflowFeatures.Add("warehousing");
        _stockFeatureIdentity.Invocations.Clear();

        await OpenWorkflowProduct();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true })).ToHaveCountAsync(0);
        await Expect(WorkflowAvailableStock).ToHaveAttributeAsync("href", $"/inventario/products/{_state.PolishProductId}/stock");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Stock", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(_app.Urls.Single() + $"/inventario/products/{_state.PolishProductId}/stock");
        var banner = Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Stock tracking needs to be enabled" });
        await Expect(banner).ToBeVisibleAsync();
        var enable = banner.GetByRole(AriaRole.Button, new() { Name = "Enable stock tracking", Exact = true });
        if (canManage)
        {
            await Expect(enable).ToBeVisibleAsync();
            await Expect(enable).ToBeEnabledAsync();
        }
        else
        {
            await Expect(enable).ToHaveCountAsync(0);
            await Expect(banner).ToContainTextAsync("Ask a tenant administrator");
        }
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Post Movement", Exact = true })).ToHaveCountAsync(0);
        await Screenshot($"product-stock-disabled-{(canManage ? "admin" : "nonadmin")}-{balancesEnabled}-{movementsEnabled}-{warehousingEnabled}-1920");
        if (!warehousingEnabled)
        {
            _data.Verify(x => x.Query<Warehouse>(), Times.Never());
            _data.Verify(x => x.Query<InventoryLocation>(), Times.Never());
        }
        _state.BasicStockRequests.Should().BeEmpty();
        _stockFeatureIdentity.VerifyNoOtherCalls();
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [TestCase(false, 1920, 1080)]
    [TestCase(true, 1920, 1080)]
    [TestCase(false, 390, 844)]
    [TestCase(true, 390, 844)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_BasicStock_BaseAndVariantUseWrapperAndRefreshDetail(bool variant, int width, int height)
    {
        PrepareProductWorkflow(hasVariant: true);
        await Page.SetViewportSizeAsync(width, height);
        await OpenWorkflowProduct();
        var dialog = await OpenBasicStock(variant);
        await dialog.GetByLabel("Quantity to add", new() { Exact = true }).FillAsync("2.5");
        await dialog.GetByLabel("Reference or reason", new() { Exact = true }).FillAsync("  Received delivery  ");
        _state.BasicStockRequests.Should().BeEmpty("opening and editing the dialog must not post stock");
        await Screenshot($"product-basic-stock-{(variant ? "variant" : "base")}-{width}");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(WorkflowAvailableStock).ToHaveTextAsync("2.5");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Synthetic QA product", Exact = true })).ToBeVisibleAsync();

        var request = _state.BasicStockRequests.Should().ContainSingle().Subject;
        request.ProductId.Should().Be(_state.PolishProductId);
        request.ProductVariationId.Should().Be(variant ? _state.PolishVariantId : null);
        request.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
        request.Quantity.Should().Be(2.5m);
        request.Reason.Should().Be("Received delivery");
        request.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
        _data.Verify(x => x.Query<Product>(), Times.AtLeast(2), "successful posting must refresh product detail");
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_BasicStock_FailedResponseOrExceptionRetriesIdenticalRequest(bool throws)
    {
        PrepareProductWorkflow(hasVariant: true);
        _state.FailNextBasicStock = true;
        _state.ThrowNextBasicStock = throws;
        await OpenWorkflowProduct();
        var dialog = await OpenBasicStock(variant: true);
        await dialog.GetByLabel("Quantity to add", new() { Exact = true }).FillAsync("3.25");
        await dialog.GetByLabel("Reference or reason", new() { Exact = true }).FillAsync("Retry delivery");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(throws
            ? "The result could not be confirmed" : "Stock could not be added");
        await Expect(dialog.GetByLabel("Quantity to add", new() { Exact = true })).ToBeDisabledAsync();
        await Expect(dialog.GetByLabel("Reference or reason", new() { Exact = true })).ToBeDisabledAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Variant", Exact = true })).ToBeDisabledAsync();
        var original = _state.BasicStockRequests.Should().ContainSingle().Subject;
        original.ProductVariationId.Should().Be(_state.PolishVariantId);
        original.Quantity.Should().Be(3.25m);
        original.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
        await Screenshot($"product-basic-stock-retry-{(throws ? "exception" : "response")}");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(WorkflowAvailableStock).ToHaveTextAsync("3.25");
        var attempts = _state.BasicStockRequests.ToArray();
        attempts.Should().HaveCount(2);
        attempts[1].Should().BeEquivalentTo(original, "a retry must preserve tenant, product, variant, quantity, reason and key");
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [Test]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_BasicStock_NewReceiptGetsNewKeyAndCancelDoesNotPost()
    {
        PrepareProductWorkflow();
        await OpenWorkflowProduct();
        var dialog = await OpenBasicStock(variant: false);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        _state.BasicStockRequests.Should().BeEmpty();
        for (var i = 0; i < 2; i++)
        {
            dialog = await OpenBasicStock(variant: false);
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(WorkflowAvailableStock).ToHaveTextAsync((i + 1).ToString());
        }
        var requests = _state.BasicStockRequests.ToArray();
        requests.Should().HaveCount(2);
        requests[1].IdempotencyKey.Should().NotBe(requests[0].IdempotencyKey);
        requests.Should().OnlyContain(x => x.ProductVariationId == null && x.Quantity == 1);
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [TestCase(false, 1920, 1080)]
    [TestCase(true, 1920, 1080)]
    [TestCase(true, 390, 844)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_CreateProduct_UsesReturnedIdAndSelectedOrInlineCategory(bool inlineCategory, int width, int height)
    {
        PrepareProductWorkflow(defaultAvailable: false);
        if (!inlineCategory)
            _state.WorkflowCategories.Add(new ProductCategory
            {
                Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Name = "Existing category", IsEnabled = true
            });
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-products-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Product", Exact = true }).ClickAsync();
        var productDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create Product", Exact = true });
        await Expect(productDialog).ToBeVisibleAsync();
        await productDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("New workflow product");
        await productDialog.GetByLabel("SKU", new() { Exact = true }).FillAsync("WORKFLOW-1");
        await productDialog.GetByLabel("Amount in Singapore Dollar", new() { Exact = true }).FillAsync("23.50");
        await productDialog.GetByLabel("Description", new() { Exact = true }).FillAsync("Product created through wrapper");
        await Expect(productDialog.GetByRole(AriaRole.Switch, new() { Name = "Available", Exact = true })).Not.ToBeCheckedAsync();

        if (inlineCategory)
        {
            await productDialog.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = "Create New", Exact = true }).ClickAsync();
            var categoryDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create Category", Exact = true });
            await Expect(categoryDialog).ToBeVisibleAsync();
            await categoryDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("  Inline category  ");
            await categoryDialog.GetByLabel("Description", new() { Exact = true }).FillAsync("Created inside product picker");
            await Screenshot($"product-inline-category-{width}");
            await categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true }).ClickAsync();
            await Expect(categoryDialog).ToHaveCountAsync(0);
            await Expect(productDialog.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true })).ToContainTextAsync("Inline category");
            await Expect(productDialog.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("New workflow product");
            var category = _state.CategoryCreates.Should().ContainSingle().Subject;
            category.Name.Should().Be("Inline category");
            category.TenantId.Should().Be(_state.FirstTenant);
        }

        await Screenshot($"product-create-workflow-{(inlineCategory ? "inline" : "existing")}-{width}");
        await productDialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(_app.Urls.Single() + $"/inventario/products/{_state.CreatedWorkflowProductId}");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "New workflow product", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Synthetic QA product", Exact = true })).ToHaveCountAsync(0);
        var request = _state.ProductCreates.Should().ContainSingle().Subject;
        request.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
        request.CategoryId.Should().Be(_state.WorkflowCategories.Single().Id);
        request.Name.Should().Be("New workflow product");
        request.SKU.Should().Be("WORKFLOW-1");
        request.Price.Should().Be(23.50m);
        request.IsAvailable.Should().BeFalse();
        request.StockQuantity.Should().Be(0, "catalog creation must not bypass the stock posting wrapper");
        _data.Verify(x => x.Add(It.IsAny<Product>()), Times.Never());
        _data.Verify(x => x.Add(It.IsAny<ProductCategory>()), Times.Never(), "category writes use their own tenant-bound context");
        _data.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        _state.BasicStockRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    private void PrepareProductWorkflow(bool hasVariant = false, bool defaultAvailable = true)
    {
        _state.ProductWorkflow = true;
        _state.ProductWorkflowHasVariant = hasVariant;
        _state.HasExistingConfiguration = true;
        _state.DefaultProductAvailable = defaultAvailable;
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Code = "STOCK", Name = "My stock", IsEnabled = true };
        var location = new InventoryLocation { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = warehouse.Id, Code = "STOCK", Name = "General stock", IsEnabled = true, IsPickable = true };
        _state.Warehouses.Add(warehouse);
        _state.Locations.Add(location);
        _state.Saved[_state.ActiveTenant] = _state.Get(_state.ActiveTenant) with
        {
            Mode = InventarioSetupMode.Basic, CompletedAt = DateTime.UtcNow, WarehouseId = warehouse.Id, LocationId = location.Id
        };
        _data.Invocations.Clear();
    }

    private async Task OpenWorkflowProduct()
    {
        await Page.GotoAsync(_app.Urls.Single() + $"/inventario/products/{_state.PolishProductId}");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Synthetic QA product", Exact = true })).ToBeVisibleAsync();
    }

    private ILocator WorkflowAvailableStock => Page.GetByText("Available Stock", new() { Exact = true })
        .Locator("..").GetByRole(AriaRole.Link);

    private async Task<ILocator> OpenBasicStock(bool variant)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add stock", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        if (variant)
        {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Variant", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = "Large", Exact = true }).ClickAsync();
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Variant", Exact = true })).ToContainTextAsync("Large");
        }
        return dialog;
    }

    private void AssertNoDirectStockWrites()
    {
        _data.Verify(x => x.Add(It.IsAny<StockBalance>()), Times.Never());
        _data.Verify(x => x.Update(It.IsAny<StockBalance>()), Times.Never());
        _data.Verify(x => x.Add(It.IsAny<InventoryMovement>()), Times.Never());
        _data.Verify(x => x.Update(It.IsAny<Product>()), Times.Never());
        _data.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    private void ConfigureProductWorkflow(Mock<IInventarioServiceWrapper> wrapper, Mock<IDataContext> data)
    {
        wrapper.Setup(x => x.PostBasicProductStock(It.IsAny<PostBasicProductStockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostBasicProductStockRequest request, CancellationToken _) => _state.PostBasicStock(request));
        wrapper.Setup(x => x.CreateProduct(It.IsAny<CreateProductRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateProductRequest request, CancellationToken _) => _state.CreateWorkflowProduct(request));
        wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<string>> { HttpStatusCode = HttpStatusCode.OK, Response = [] });
        wrapper.As<IDataContextServiceWrapper>().Setup(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] bytes, CancellationToken _) => _state.SaveWorkflowCategory(bytes));
        wrapper.As<IDataContextServiceWrapper>().Setup(x => x.ExecuteQueryAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] bytes, CancellationToken _) => _state.FindWorkflowCategory(bytes));
        ConfigureCombinationWorkflow(wrapper, data);
    }
}

public sealed partial class SetupBrowserState
{
    public HashSet<string> DisabledWorkflowFeatures { get; } = [];
    public bool ProductWorkflow { get; set; }
    public bool ProductWorkflowHasVariant { get; set; }
    public bool FailNextBasicStock { get; set; }
    public bool ThrowNextBasicStock { get; set; }
    public Guid CreatedWorkflowProductId { get; private set; }
    public ConcurrentQueue<PostBasicProductStockRequest> BasicStockRequests { get; } = new();
    public ConcurrentQueue<CreateProductRequest> ProductCreates { get; } = new();
    public ConcurrentQueue<ProductCategory> CategoryCreates { get; } = new();
    public List<ProductCategory> WorkflowCategories { get; } = [];
    public List<Product> WorkflowProducts { get; } = [];
    public List<StockBalance> WorkflowBalances { get; } = [];

    public List<Product> BrowserProducts() =>
    [
        new Product { Id = PolishProductId, TenantId = ActiveTenant, Name = "Synthetic QA product", Price = 17,
            StockQuantity = 7 + WorkflowBalances.Where(x => x.ProductId == PolishProductId).Sum(x => x.OnHandQuantity) },
        .. WorkflowProducts
    ];

    private void ResetProductWorkflow()
    {
        ResetProductRetries();
        ResetCombinationWorkflow();
        DisabledWorkflowFeatures.Clear();
        ProductWorkflow = false; ProductWorkflowHasVariant = false; FailNextBasicStock = false; ThrowNextBasicStock = false;
        CreatedWorkflowProductId = Guid.NewGuid();
        BasicStockRequests.Clear(); ProductCreates.Clear(); CategoryCreates.Clear();
        WorkflowCategories.Clear(); WorkflowProducts.Clear(); WorkflowBalances.Clear();
    }

    public QueryResponse<StockPostingResponse> PostBasicStock(PostBasicProductStockRequest request)
    {
        BasicStockRequests.Enqueue(request);
        if (PostedWorkflowStock.TryGetValue(request.IdempotencyKey, out var posted)) return posted;
        var failAfterCommit = FailNextBasicStock && CommitStockBeforeFailure;
        if (FailNextBasicStock && !failAfterCommit)
        {
            FailNextBasicStock = false;
            if (ThrowNextBasicStock) { ThrowNextBasicStock = false; throw new TimeoutException("Synthetic uncertain response"); }
            return new() { HttpStatusCode = HttpStatusCode.ServiceUnavailable };
        }
        var balance = WorkflowBalances.SingleOrDefault(x => x.ProductId == request.ProductId && x.ProductVariationId == request.ProductVariationId);
        if (balance is null)
        {
            balance = new StockBalance { Id = Guid.NewGuid(), TenantId = request.Metadata.RequestedTenantId!.Value,
                ProductId = request.ProductId, ProductVariationId = request.ProductVariationId,
                WarehouseId = Warehouses.Single().Id, LocationId = Locations.Single().Id };
            WorkflowBalances.Add(balance);
        }
        balance.OnHandQuantity += request.Quantity;
        balance.AvailableQuantity = balance.OnHandQuantity;
        var response = new QueryResponse<StockPostingResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new StockPostingResponse(balance.Id, balance.ProductId,
            balance.ProductVariationId, balance.WarehouseId, balance.LocationId, balance.OnHandQuantity, 0, balance.AvailableQuantity)
            { IdempotencyKey = request.IdempotencyKey } };
        PostedWorkflowStock[request.IdempotencyKey] = response;
        if (failAfterCommit)
        {
            FailNextBasicStock = false;
            throw new TimeoutException("Synthetic response lost after commit");
        }
        return response;
    }

    public CmdResponse<ProductResponse> CreateWorkflowProduct(CreateProductRequest request)
    {
        ProductCreates.Enqueue(request);
        var product = new Product { Id = CreatedWorkflowProductId, TenantId = request.Metadata.RequestedTenantId!.Value,
            Name = request.Name, SKU = request.SKU, CategoryId = request.CategoryId, Price = request.Price,
            Description = request.Description, IsAvailable = request.IsAvailable ?? true, IsEnabled = true };
        WorkflowProducts.Add(product);
        return new() { HttpStatusCode = HttpStatusCode.Created, Response = ProductResponse.FromProduct(product) };
    }
}
