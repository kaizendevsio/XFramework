using FluentAssertions;
using Microsoft.Playwright;

namespace Portal.E2ETests;

public sealed partial class InventoryReportsE2ETests
{
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
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"overview-{width}.png"), FullPage = true });
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
