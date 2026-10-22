using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using Inventario.Integration.Drivers;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Portal.E2ETests;

public sealed partial class InventoryReportsE2ETests
{
    [TestCase(1920, 1080)]
    [TestCase(390, 844)]
    public async Task ProductSales_DateRangeSummary_PagesWithoutRequiredSearchOrNestedHeader(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await OpenProductSales();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sales Transactions", Exact = true })).ToHaveCountAsync(1);
        await Expect(Page.GetByText("Sold product 01", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Sales Transaction History", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByLabel("Product search", new() { Exact = true })).ToHaveCountAsync(0);
        var first = state.ProductSalesRequests.Last();
        first.Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        first.ToUtcExclusive.Subtract(first.FromUtc).TotalDays.Should().Be(30);
        first.Take.Should().Be(20);
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-product-sales");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"product-sales-{width}.png");
        await Page.ScreenshotAsync(new() { Path = path, Animations = ScreenshotAnimations.Disabled });
        TestContext.AddTestAttachment(path);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Sold product 21", new() { Exact = true })).ToBeVisibleAsync();
        state.ProductSalesRequests.Last().Skip.Should().Be(20);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Filter Product", Exact = true }).ClickAsync();
        await Page.GetByPlaceholder("Enter value...").FillAsync("Sold product 25");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).Last.ClickAsync();
        await Expect(Page.GetByText("Sold product 25", new() { Exact = true })).ToBeVisibleAsync();
        state.ProductSalesRequests.Last().ProductFilter.Should().Be("Sold product 25");
        state.ProductSalesRequests.Last().Skip.Should().Be(0);
    }

    [Test]
    public async Task ProductSales_DatePreset_RefreshesServerRange()
    {
        await OpenProductSales();
        await Expect(Page.GetByText("Sold product 01", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sales date range UTC" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Today", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Wait(() => state.ProductSalesRequests.Last().ToUtcExclusive - state.ProductSalesRequests.Last().FromUtc == TimeSpan.FromDays(1));
        state.ProductSalesRequests.Last().FromUtc.Should().Be(DateTime.UtcNow.Date);
        state.ProductSalesRequests.Last().Skip.Should().Be(0);
    }

    [Test]
    public async Task ProductSales_FailureAndRefresh_DoNotPresentFailureAsEmptySuccess()
    {
        state.ProductSalesStatus = HttpStatusCode.Forbidden;
        await OpenProductSales();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Could not load sales summary");
        state.ProductSalesStatus = HttpStatusCode.OK;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Sold product 01", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ProductSales_TenantChange_DiscardsOldResults()
    {
        await OpenProductSales();
        await Expect(Page.GetByText("Sold product 01", new() { Exact = true })).ToBeVisibleAsync();
        var initialRequests = state.ProductSalesRequests.Count;
        var hold = state.HoldProductSales = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
            await Wait(() => state.ProductSalesRequests.Count > initialRequests);
            state.SelectedTenant = state.SecondTenant;
            state.Tenant.Raise(x => x.OnChanged += null);
            hold.TrySetResult();
            await Expect(Page.GetByText("South product 01", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByText("Sold product 01", new() { Exact = true })).ToHaveCountAsync(0);
            state.ProductSalesRequests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        }
        finally { hold.TrySetResult(); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ProductSales_InventorySource_PreservesDirectSalesWithoutPOSAndPagesByTenant(bool posEnabled)
    {
        state.PosSalesEnabled = posEnabled;
        await OpenProductSales();
        if (posEnabled) await Page.GetByRole(AriaRole.Tab, new() { Name = "Inventory sales", Exact = true }).ClickAsync();
        else await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "POS product sales" })).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Direct product 01", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Recorded revenue", new() { Exact = true })).ToBeVisibleAsync();
        state.InventorySalesRequests.Last().Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        if (!posEnabled) state.ProductSalesRequests.Should().BeEmpty();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Direct product 21", new() { Exact = true })).ToBeVisibleAsync();
        state.InventorySalesRequests.Last().Skip.Should().Be(20);
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByText("South direct 01", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Direct product 21", new() { Exact = true })).ToHaveCountAsync(0);
        state.InventorySalesRequests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
    }

    private async Task OpenProductSales()
    {
        var response = await Page.GotoAsync(app.Urls.Single() + "/inventario/transactions");
        response.Should().NotBeNull();
        response!.Status.Should().Be(200, "the fixture must map the Transactions route");
    }

    private void ConfigureProductSalesFixture(IServiceCollection services, Mock<IInventarioServiceWrapper> inventory)
    {
        var wrapper = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetPosProductSalesReport(It.IsAny<GetPosProductSalesReportRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetPosProductSalesReportRequest request, CancellationToken ct) => state.LoadProductSales(request));
        services.AddSingleton(wrapper.Object);
        inventory.Setup(x => x.GetInventoryProductSales(It.IsAny<GetInventoryProductSalesRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetInventoryProductSalesRequest request, CancellationToken ct) => state.LoadInventorySales(request));
    }
}

public sealed partial class InventoryReportFixtureState
{
    public bool PosSalesEnabled = true;
    public ConcurrentQueue<GetInventoryProductSalesRequest> InventorySalesRequests { get; } = new();
    public ConcurrentQueue<GetPosProductSalesReportRequest> ProductSalesRequests { get; } = new();
    public HttpStatusCode ProductSalesStatus = HttpStatusCode.OK;
    public TaskCompletionSource? HoldProductSales;
    private readonly Guid salesCurrency = Guid.NewGuid();
    public Task<QueryResponse<InventoryProductSalesPage>> LoadInventorySales(GetInventoryProductSalesRequest request)
    {
        InventorySalesRequests.Enqueue(request);
        var rows = Enumerable.Range(1, 25).Select(i => new InventoryProductSalesRow(ProductId, null,
            $"{(request.Metadata.RequestedTenantId == FirstTenant ? "Direct product" : "South direct")} {i:00}", "Standard", $"SKU-{i:00}", 1002, 3757.5m))
            .Where(x => string.IsNullOrEmpty(request.ProductFilter) || x.ProductName.Contains(request.ProductFilter)).ToList();
        return Task.FromResult(new QueryResponse<InventoryProductSalesPage> { HttpStatusCode = HttpStatusCode.OK,
            Response = new(rows.Count, rows.Skip(request.Skip).Take(request.Take).ToList()) });
    }
    public async Task<QueryResponse<PosProductSalesReportResponse>> LoadProductSales(GetPosProductSalesReportRequest request)
    {
        var hold = Interlocked.Exchange(ref HoldProductSales, null);
        ProductSalesRequests.Enqueue(request);
        if (hold is not null) await hold.Task;
        var rows = Enumerable.Range(1, 25).Select(i => new PosProductSalesRow(ProductId, null, salesCurrency,
            $"{(request.Metadata.RequestedTenantId == FirstTenant ? "Sold" : "South")} product {i:00}", "Standard", $"SKU-{i:00}", 1002, 9018))
            .Where(x => string.IsNullOrEmpty(request.ProductFilter) || x.ProductName.Contains(request.ProductFilter)).ToList();
        return new() { HttpStatusCode = ProductSalesStatus, Response = ProductSalesStatus != HttpStatusCode.OK ? null : new(
            request.Metadata.RequestedTenantId!.Value, request.FromUtc, request.ToUtcExclusive, rows.Count,
            rows.Skip(request.Skip).Take(request.Take).ToList(), [new(salesCurrency, "USD")]) };
    }
}
