using FluentAssertions;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Net;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Portal.E2ETests;

public sealed partial class InventoryReportsE2ETests
{
    [TestCase("On hand", "positions")]
    [TestCase("Reserved", "positions")]
    [TestCase("Available", "positions")]
    [TestCase("Stock positions", "positions")]
    [TestCase("Inbound", "movements")]
    [TestCase("Outbound", "movements")]
    public async Task Overview_MetricClick_PreservesAppliedWarehouseAndDates(string metric, string view)
    {
        await OpenScopedOverview();
        var applied = state.Requests.Last();
        // An unapplied filter must not change what a displayed metric drills into.
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Warehouse", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "All warehouses", Exact = true }).ClickAsync();
        await Page.Locator(".overview-metrics, .overview-movement-totals")
            .GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^" + metric) }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("view=" + view));
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        var request = state.Requests.Last();
        request.WarehouseId.Should().Be(state.WarehouseId);
        request.FromUtc.Should().Be(applied.FromUtc);
        request.ToUtc.Should().Be(applied.ToUtc);
        request.Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
    }

    [Test]
    public async Task Overview_LowStock_UsesServerCountAndPreservesWarehouseInPlanningLink()
    {
        await OpenScopedOverview();
        var metric = Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 1,207", Exact = true });
        await Expect(metric).ToBeEnabledAsync();
        await Expect(metric).ToHaveAttributeAsync("title", "Low-stock positions matching active reorder rules");
        state.LowStockRequests.Last().WarehouseId.Should().Be(state.WarehouseId);
        state.LowStockRequests.Last().Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        state.Requests.Last().WarehouseId.Should().Be(state.WarehouseId);
        await metric.ClickAsync();
        await Expect(Page).ToHaveURLAsync(app.Urls.Single() + $"/inventario/planning?warehouseId={state.WarehouseId}");
    }

    [Test]
    public async Task Overview_LowStockFailure_ShowsUnavailableNotZero_AndRefreshRecovers()
    {
        state.LowStockStatus = HttpStatusCode.Forbidden;
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock Unavailable", Exact = true })).ToBeDisabledAsync();
        await Expect(Page.GetByText("Low-stock count could not be loaded. Try Refresh.")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "On hand 20.00", Exact = true })).ToBeEnabledAsync();
        state.LowStockStatus = HttpStatusCode.OK; state.LowStockRows = 0;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 0", Exact = true })).ToBeEnabledAsync();
        await Expect(Page.GetByText("Low-stock count could not be loaded. Try Refresh.")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Overview_LowStockTenantChange_DiscardsStaleResponse()
    {
        var hold = state.HoldLowStock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Wait(() => !state.LowStockRequests.IsEmpty);
        state.SelectedTenant = state.SecondTenant; state.LowStockRows = 3;
        state.Tenant.Raise(x => x.OnChanged += null);
        hold.TrySetResult();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 3", Exact = true })).ToBeEnabledAsync();
        state.LowStockRequests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        state.LowStockRequests.Last().WarehouseId.Should().BeNull();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 1,207", Exact = true })).ToHaveCountAsync(0);
    }

    private async Task OpenScopedOverview()
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 1,207", Exact = true })).ToBeEnabledAsync();
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Warehouse", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Main warehouse", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Low stock 1,207", Exact = true })).ToBeEnabledAsync();
    }

    [Test]
    public async Task Overview_StockSplit_ContainsOnlyReservedAndAvailable_AndKeepsOnHandMetric()
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        var chart = Page.Locator("section[aria-label='Stock quantities'] div[_echarts_instance_]");
        await Expect(chart.Locator("svg")).ToBeVisibleAsync();
        var slices = await chart.EvaluateAsync<string[]>("""
            async element => {
                const echarts = await import('/_content/BlazorBlueprint.Components/lib/echarts/echarts.min.js');
                return echarts.getInstanceByDom(element).getOption().series.find(series => series.type === 'pie')
                    .data.map(point => `${point.name}:${point.value}`);
            }
            """);
        slices.Should().Equal("Reserved:5", "Available:15");
        await Expect(Page.GetByLabel("Current stock summary")).ToContainTextAsync("On hand20.00");
    }

    [TestCase(-1, 15, "Stock split unavailable")]
    [TestCase(5, -1, "Stock split unavailable")]
    [TestCase(0, 0, "No stock quantities")]
    [TestCase(-5, -15, "Stock split unavailable")]
    public async Task Overview_StockSplit_InvalidProportions_ShowEmptyState(int reserved, int available, string title)
    {
        state.Reserved = reserved; state.Available = available;
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        var section = Page.Locator("section[aria-label='Stock quantities']");
        await Expect(section.GetByText(title, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(section.Locator("div[_echarts_instance_]")).ToHaveCountAsync(0);
        await Expect(Page.GetByLabel("Current stock summary")).ToContainTextAsync("On hand20.00");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Overview_ReportingDisabled_ShowsUnavailableWithoutQuery_AndRecoversWhenEnabled(bool switchTenant)
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        if (switchTenant)
        {
            await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
            await Expect(Page.GetByLabel("Current stock summary")).ToBeVisibleAsync();
        }
        var before = state.Requests.Count;
        state.ReportingEnabled = false;
        if (switchTenant)
        {
            state.SelectedTenant = state.SecondTenant;
            state.Tenant.Raise(x => x.OnChanged += null);
        }
        else await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByText("Inventario Reporting disabled", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Current stock summary")).ToHaveCountAsync(0);
        await Expect(Page.Locator("div[_echarts_instance_]")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Reports", Exact = true })).ToBeDisabledAsync();
        state.Requests.Should().HaveCount(before);
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-overview");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"reporting-disabled-{switchTenant}-1920.png"), FullPage = true });
        state.ReportingEnabled = true;
        state.Tenant.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByLabel("Current stock summary")).ToBeVisibleAsync();
        state.Requests.Last().Metadata.RequestedTenantId.Should().Be(state.SelectedTenant);
    }

    [TestCase(390, 844)]
    [TestCase(1920, 1080)]
    public async Task Overview_AggregatedMetricsAndCharts_RenderWithoutOverflow_AndDrillIntoReports(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Inventory Overview", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Current stock summary")).ToContainTextAsync("20.00");
        var charts = Page.Locator("div[_echarts_instance_] svg");
        await Expect(charts).ToHaveCountAsync(2);
        await Expect(charts.First).ToBeVisibleAsync();
        (await charts.EvaluateAllAsync<bool>("charts => charts.every(s => s.getBoundingClientRect().width > 100 && s.querySelectorAll('path,rect').length >= 2)")).Should().BeTrue();
        state.Requests.Last().Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-overview");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"overview-{width}.png"), Animations = ScreenshotAnimations.Disabled });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Stock positions", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/inventario/reports\\?.*view=positions"));
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Overview_MovementChartPoint_DrillsIntoExactUtcDay()
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        var chart = Page.Locator("section[aria-label='Movement quantities'] div[_echarts_instance_]");
        await Expect(chart.Locator("svg")).ToBeVisibleAsync();
        var point = await chart.EvaluateAsync<float[]>("""
            async element => {
                const echarts = await import('/_content/BlazorBlueprint.Components/lib/echarts/echarts.min.js');
                const instance = echarts.getInstanceByDom(element);
                return instance.convertToPixel({seriesIndex: 0}, [1, 20]);
            }
            """);
        await chart.ClickAsync(new() { Position = new() { X = point[0], Y = point[1] } });
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/inventario/reports\\?.*view=movements"));
        var request = state.Requests.Last();
        request.FromUtc.Date.Should().Be(request.ToUtc.Date);
        request.FromUtc.Date.Should().Be(DateTime.UtcNow.Date.AddDays(-28));
        request.Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
    }

    [Test]
    public async Task Overview_TenantChange_ResetsDateControlsToTheLoadedScope()
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByLabel("Current stock summary")).ToBeVisibleAsync();
        var from = Page.Locator(".overview-filter").Nth(1).Locator("input");
        var through = Page.Locator(".overview-filter").Nth(2).Locator("input");
        await from.FillAsync("2026-09-01"); await from.PressAsync("Tab");
        await through.FillAsync("2026-09-07"); await through.PressAsync("Tab");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Current stock summary")).ToBeVisibleAsync();
        state.Requests.Last().FromUtc.Date.Should().Be(new DateTime(2026, 9, 1));
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByText("South Widget", new() { Exact = true })).ToBeVisibleAsync();
        var request = state.Requests.Last();
        request.Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        await Expect(from).ToHaveValueAsync(request.FromUtc.ToString("yyyy-MM-dd"));
        await Expect(through).ToHaveValueAsync(request.ToUtc.ToString("yyyy-MM-dd"));
    }

    [Test]
    public async Task Overview_TenantChange_ClearsOldData_AndDenialHidesMetrics()
    {
        await Page.GotoAsync(app.Urls.Single() + "/inventario/overview");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true })).ToBeVisibleAsync();
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByText("South Widget", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true })).ToHaveCountAsync(0);
        state.Requests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        state.Status = System.Net.HttpStatusCode.Forbidden;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Could not open the overview. Check your report permission and tenant access.")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Current stock summary")).ToHaveCountAsync(0);
    }
}

public sealed partial class InventoryReportFixtureState
{
    public ConcurrentQueue<GetLowStockCountRequest> LowStockRequests { get; } = new();
    public int LowStockRows = 1207;
    public HttpStatusCode LowStockStatus = HttpStatusCode.OK;
    public TaskCompletionSource? HoldLowStock;

    public async Task<QueryResponse<int>> LoadLowStock(GetLowStockCountRequest request, CancellationToken ct)
    {
        var count = LowStockRows;
        var hold = Interlocked.Exchange(ref HoldLowStock, null);
        LowStockRequests.Enqueue(request);
        if (hold is not null) await hold.Task; // Ignore cancellation to test the page's revision guard.
        return new()
        {
            HttpStatusCode = LowStockStatus,
            Response = LowStockStatus != HttpStatusCode.OK ? 0 : count
        };
    }
}
