using System.Net;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using IdentityServer.Domain.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
public sealed class FinancialReportsE2ETests : PageTest
{
    private WebApplication app = null!;
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid currencyId = Guid.NewGuid();
    private WalletFinancialReportRequest? latest;
    private readonly List<string> errors = [];
    private Mock<IWalletsServiceWrapper> wrapper = null!;
    private Mock<IPortalModuleAvailability> modules = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent!;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(x => {
            x.DetailedErrors = true; x.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero;
        });
        builder.Services.AddBlazorBlueprintComponents(configureTheme: theme =>
        {
            theme.DefaultBaseColor = BaseColor.Slate;
            theme.DefaultPrimaryColor = PrimaryColor.Blue;
            theme.DefaultRadius = 0.5;
            theme.DefaultDarkMode = false;
            theme.DetectSystemPreference = false;
        });
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(x => x.SelectedTenantId == tenantId && x.SelectedTenantName == "Report fixture"));
        modules = new Mock<IPortalModuleAvailability>();
        modules.Setup(x => x.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        modules.Setup(x => x.IsFeatureEnabled(TenantModuleFeatureKeys.WalletsReporting)).Returns(true);
        builder.Services.AddSingleton(modules.Object);
        wrapper = new();
        wrapper.Setup(x => x.WalletFinancialReport(It.IsAny<WalletFinancialReportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletFinancialReportRequest request, CancellationToken _) => {
                latest = request;
                return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() {
                    TenantId = tenantId, GeneratedAt = DateTime.UtcNow,
                    Currencies = [new() { CurrencyId = currencyId, Currency = "EUR", WalletCount = 2, Balance = 123, Credits = 17, Debits = 5 }],
                    DailyActivity = [new() { CurrencyId = currencyId, Date = request.From, Credits = 17, Debits = 5 }]
                }};
            });
        builder.Services.AddSingleton(wrapper.Object);
        app = builder.Build();
        app.UseStaticFiles();
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal.Shared/wwwroot")), RequestPath = "/_content/XFramework.Portal.Shared" });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal.Shared/wwwroot")), RequestPath = "/finance-fixture-pdf" });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal/wwwroot")), RequestPath = "/portal-assets" });
        app.UseAntiforgery();
        app.MapGet("/_content/XFramework.Portal.Shared/reports/report-export.js", () => Microsoft.AspNetCore.Http.Results.Text(
            "import {exportPdf as exportSnapshot, copyReportLink} from '/finance-fixture-pdf/reports/report-export.js'; export {copyReportLink}; export async function exportPdf(model){window.financialPdfSnapshot=model;return exportSnapshot(model);}", "text/javascript"));
        app.MapRazorComponents<FinancialReportFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { await app.StopAsync(); await app.DisposeAsync(); }

    [SetUp]
    public void Reset() { latest = null; errors.Clear(); Page.PageError += (_, error) => errors.Add(error); }

    [TearDown]
    public async Task Diagnostics()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            TestContext.Progress.WriteLine(await Page.Locator("body").InnerTextAsync() + "\n" + string.Join("\n", errors));
    }

    [TestCase(390, 844)]
    [TestCase(1920, 1080)]
    public async Task ActualReport_RendersCharts_UsesExplicitTenant_ExportsPdf(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        var response = await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-09-01&through=2026-09-07");
        response!.Status.Should().Be(200, await Page.ContentAsync());
        (await Page.APIRequest.GetAsync(app.Urls.Single() + "/XFramework.Portal.styles.css")).Status.Should().Be(200);
        (await Page.APIRequest.GetAsync(app.Urls.Single() + "/portal-assets/css/utilities.css")).Status.Should().Be(200);
        await Expect(Page.Locator("link[href='portal-assets/css/app.css'] + link[href='portal-assets/css/utilities.css']"))
            .ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - EUR" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Lines", Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.Locator("table")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Switch)).ToHaveCountAsync(0);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-base-color", "slate");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-primary-color", "blue");
        await Expect(Page.Locator("html")).ToHaveCSSAsync("--radius", "0.5rem");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('dark')")).Should().BeFalse();
        latest!.Metadata.RequestedTenantId.Should().Be(tenantId);
        latest.From.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        latest.ToExclusive.Should().Be(new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));
        var chart = Page.Locator("div[_echarts_instance_] svg");
        await Expect(chart).ToBeVisibleAsync();
        (await chart.EvaluateAsync<bool>("s => s.getBoundingClientRect().width > 100 && s.querySelectorAll('path,rect').length > 5")).Should().BeTrue();
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true }).ClickAsync());
        var pdfPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", $"financial-report-{width}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
        await download.SaveAsAsync(pdfPath);
        var bytes = await File.ReadAllBytesAsync(pdfPath);
        System.Text.Encoding.ASCII.GetString(bytes).Should().StartWith("%PDF-");
        bytes.Length.Should().BeGreaterThan(3000);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetDirectoryName(pdfPath)!, $"financial-summary-{width}.png"), FullPage = true });
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        foreach (var view in new[] { "Balances", "Activity", "Daily detail" })
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = view, Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = view, Exact = true })).ToHaveAttributeAsync("data-state", "active");
            await Expect(Page.Locator("table")).ToHaveCountAsync(1);
            await Expect(Page.Locator("div[_echarts_instance_]")).ToHaveCountAsync(0);
            if (width == 1920)
                await Page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetDirectoryName(pdfPath)!, $"financial-{view.Replace(' ', '-')}-1920.png"), FullPage = true });
            (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        }
        errors.Should().BeEmpty();
    }

    [TestCase("balances", "Balances")]
    [TestCase("activity", "Activity")]
    [TestCase("daily", "Daily detail")]
    public async Task Pdf_FromTableTab_IncludesSnapshotChartsWithoutMountingSummary(string view, string label)
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-09-01&through=2026-09-07&view={view}");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = label, Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.Locator("div[_echarts_instance_]")).ToHaveCountAsync(0);
        var request = latest;
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true }).ClickAsync());
        (await Page.EvaluateAsync<string[]>("window.financialPdfSnapshot.charts.map(chart => chart.title)"))
            .Should().Equal("Daily credits - EUR", "Daily debits (including fees) - EUR");
        (await Page.EvaluateAsync<decimal[]>("window.financialPdfSnapshot.charts.map(chart => chart.values[0])")).Should().Equal(17m, 5m);
        (await Page.EvaluateAsync<int>("window.financialPdfSnapshot.charts[0].labels.length")).Should().Be(7);
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", $"financial-{view}-with-charts.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await download.SaveAsAsync(path);
        var bytes = await File.ReadAllBytesAsync(path);
        System.Text.Encoding.ASCII.GetString(bytes).Should().StartWith("%PDF-");
        bytes.Length.Should().BeGreaterThan(3000);
        latest.Should().BeSameAs(request);
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = label, Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.Locator("div[_echarts_instance_]")).ToHaveCountAsync(0);
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task SharedReport_DifferentTenant_FailsClosedWithoutRequest()
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={Guid.NewGuid()}");
        await Expect(Page.GetByText("Select the report's tenant to view this link.")).ToBeVisibleAsync();
        latest.Should().BeNull();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeDisabledAsync();
    }

    [TestCase("currency=invalid")]
    [TestCase("tenant=invalid")]
    [TestCase("chart=invalid")]
    [TestCase("view=invalid")]
    [TestCase("day=2026-08-01")]
    public async Task SharedReport_InvalidFilter_FailsClosedWithoutRequest(string filter)
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?from=2026-09-01&through=2026-09-07&{filter}");
        await Expect(Page.GetByText("The report link contains an invalid filter.")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        latest.Should().BeNull();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task CurrencyChartAndDay_SharedLink_RestoresSelection_AndExportsOnlySelectedCurrency()
    {
        var from = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var usd = Guid.NewGuid();
        wrapper.Setup(x => x.WalletFinancialReport(It.Is<WalletFinancialReportRequest>(r => r.From == from), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletFinancialReportRequest request, CancellationToken _) => {
                latest = request;
                return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() {
                    TenantId = tenantId, GeneratedAt = DateTime.UtcNow,
                    Currencies = [new() { CurrencyId = currencyId, Currency = "EUR", Credits = 17, Debits = 5 }, new() { CurrencyId = usd, Currency = "USD", Credits = 900 }],
                    DailyActivity = [new() { CurrencyId = currencyId, Date = from, Credits = 17, Debits = 5 }, new() { CurrencyId = usd, Date = from, Credits = 900 }]
                }};
            });
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-04-01&through=2026-04-07&currency={currencyId}&chart=line&day=2026-04-01&view=summary");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Lines", Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - USD" })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Daily detail", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily detail (UTC) - 2026-04-01", Exact = true })).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => { window.sharedReportUrl = undefined; navigator.clipboard.writeText = async value => { window.sharedReportUrl = value; }; }");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Copy link", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("typeof window.sharedReportUrl === 'string'");
        var url = await Page.EvaluateAsync<string>("window.sharedReportUrl");
        url.Should().Contain($"currency={currencyId}").And.Contain("view=daily").And.Contain("day=2026-04-01").And.NotContain("chart=").And.NotContain("token");
        await Expect(Page.GetByRole(AriaRole.Textbox, new() { Name = "Protected report link" })).ToHaveValueAsync(url);
        await Page.GotoAsync(url);
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Daily detail", Exact = true })).ToHaveAttributeAsync("data-state", "active");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily detail (UTC) - 2026-04-01", Exact = true })).ToBeVisibleAsync();
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true }).ClickAsync());
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "financial-filtered-snapshot.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await download.SaveAsAsync(path);
        (await File.ReadAllBytesAsync(path)).Length.Should().BeGreaterThan(3000);
        await Page.GetByRole(AriaRole.Button, new() { Name = "All days", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily detail (UTC)", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Currency", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "All currencies", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - EUR" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - USD" })).ToBeVisibleAsync();
        errors.Should().BeEmpty();
    }

    [TestCase("line")]
    [TestCase("bar")]
    public async Task ChartPoint_SelectsCurrencyAndUtcDay_WithoutAnotherBackendQuery(string chartType)
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-09-01&through=2026-09-07&chart={chartType}");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - EUR" })).ToBeVisibleAsync();
        var request = latest;
        var chart = Page.Locator("div[_echarts_instance_]");
        await Expect(chart.Locator("svg")).ToBeVisibleAsync();
        await chart.ScrollIntoViewIfNeededAsync();
        if (chartType == "bar")
        {
        var point = await chart.EvaluateAsync<float[]>("""
            element => {
                const bar = [...element.querySelectorAll("svg path[fill='#16a34a']")]
                    .find(path => path.getBoundingClientRect().height > 30);
                const bounds = bar.getBoundingClientRect();
                return [bounds.x + bounds.width / 2, bounds.y + bounds.height / 2];
            }
            """);
        await Page.Mouse.ClickAsync(point[0], point[1]);
        }
        else
        {
            var point = await chart.EvaluateAsync<float[]>("""
                async element => {
                    const echarts = await import('/_content/BlazorBlueprint.Components/lib/echarts/echarts.min.js');
                    return echarts.getInstanceByDom(element).convertToPixel({seriesIndex: 0}, [0, 17]);
                }
                """);
            await chart.ClickAsync(new() { Position = new() { X = point[0], Y = point[1] } });
        }
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily detail (UTC) - 2026-09-01", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Daily detail", Exact = true })).ToHaveAttributeAsync("data-state", "active");
        latest.Should().BeSameAs(request);
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task FiltersAndShareLink_PreserveAppliedUtcRange_AndCalendarRemainsResponsive()
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-09-01&through=2026-09-07");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        var dates = Page.Locator(".financial-report-filter input");
        await dates.Nth(0).FillAsync("2026-09-03"); await dates.Nth(0).PressAsync("Tab");
        await dates.Nth(1).FillAsync("2026-09-05"); await dates.Nth(1).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Activity", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Activity: 2026-09-03 to 2026-09-05 (UTC)" })).ToBeVisibleAsync();
        latest!.From.Should().Be(new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        latest.ToExclusive.Should().Be(new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        await Page.EvaluateAsync("() => { window.sharedReportUrl = undefined; navigator.clipboard.writeText = async value => { window.sharedReportUrl = value; }; }");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Copy link", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("typeof window.sharedReportUrl === 'string'");
        (await Page.EvaluateAsync<string>("window.sharedReportUrl")).Should().EndWith($"finance/reports?tenant={tenantId}&from=2026-09-03&through=2026-09-05&view=activity");
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Page.Locator(".financial-report-filter button").First.ClickAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Financial Reports", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeEnabledAsync();
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task LiveReport_RecoversFromFailedRefresh_UsingAppliedScope()
    {
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        WalletFinancialReportRequest? liveLatest = null;
        var failNext = false;
        wrapper.Setup(x => x.WalletFinancialReport(It.Is<WalletFinancialReportRequest>(r => r.From == from), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletFinancialReportRequest request, CancellationToken _) => {
                liveLatest = request;
                if (failNext) { failNext = false; return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.ServiceUnavailable }; }
                return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() { TenantId = tenantId, GeneratedAt = DateTime.UtcNow } };
            });
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-08-01&through=2026-08-07");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        var initial = liveLatest;
        failNext = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        while (ReferenceEquals(initial, liveLatest)) await Task.Delay(100, timeout.Token);
        await Expect(Page.GetByText("Financial data could not be loaded. Check your report permissions and try again.")).ToBeVisibleAsync();
        var failed = liveLatest;
        while (ReferenceEquals(failed, liveLatest)) await Task.Delay(100, timeout.Token);
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        liveLatest!.Metadata.RequestedTenantId.Should().Be(tenantId);
        liveLatest.From.Should().Be(initial!.From);
        liveLatest.ToExclusive.Should().Be(initial.ToExclusive);
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task InvalidSharedDate_RemainsBlockedAfterRefresh()
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=invalid&through=2026-09-07");
        await Expect(Page.GetByText("The report link contains an invalid date.")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("The report link contains an invalid date.")).ToBeVisibleAsync();
        latest.Should().BeNull();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task InvalidQuery_DiscardsLateResponseFromPreviousScope()
    {
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<CmdResponse<WalletFinancialReportResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        wrapper.Setup(x => x.WalletFinancialReport(It.Is<WalletFinancialReportRequest>(r => r.From == from), It.IsAny<CancellationToken>()))
            .Returns(() => { started.TrySetResult(); return pending.Task; });
        try
        {
            await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-07-01&through=2026-07-07");
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Page.EvaluateAsync("Blazor.navigateTo('/finance/reports?from=invalid')");
            await Expect(Page.GetByText("The report link contains an invalid date.")).ToBeVisibleAsync();
        }
        finally
        {
            pending.TrySetResult(new() { HttpStatusCode = HttpStatusCode.OK, Response = new() { TenantId = tenantId, GeneratedAt = DateTime.UtcNow } });
        }
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeDisabledAsync();
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task ModuleRefresh_CancelsPendingReport_RecoversAndDisposesWithoutDisconnect()
    {
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        wrapper.Setup(x => x.WalletFinancialReport(It.Is<WalletFinancialReportRequest>(r => r.From == from), It.IsAny<CancellationToken>()))
            .Returns(async (WalletFinancialReportRequest _, CancellationToken token) => {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    started.TrySetResult();
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
                return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.OK,
                    Response = new() { TenantId = tenantId, GeneratedAt = DateTime.UtcNow } };
            });
        Page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-06-01&through=2026-06-07");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Run(() => modules.Raise(x => x.OnChanged += null));
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeEnabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close report", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open report", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        calls.Should().BeGreaterThanOrEqualTo(3);
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task RefreshControls_AreJoined_AndAutoRefreshMenuDismissesOutside()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync(app.Urls.Single() + "/finance/reports");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        var group = Page.GetByRole(AriaRole.Group, new() { Name = "Refresh controls" });
        var refresh = group.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });
        var options = group.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true });
        var left = (await refresh.BoundingBoxAsync())!;
        var right = (await options.BoundingBoxAsync())!;
        Math.Abs(right.X - (left.X + left.Width)).Should().BeLessThanOrEqualTo(1);
        await options.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Auto-refresh every 30 seconds" })).ToBeCheckedAsync();
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "financial-refresh-menu-1920.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Financial Reports", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitemcheckbox)).ToHaveCountAsync(0);
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task AutoRefreshMenu_DisableStopsPolling_AndEnableResumesIt()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        wrapper.Setup(x => x.WalletFinancialReport(It.Is<WalletFinancialReportRequest>(r => r.From == from), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                Interlocked.Increment(ref calls);
                return new CmdResponse<WalletFinancialReportResponse> { HttpStatusCode = HttpStatusCode.OK,
                    Response = new() { TenantId = tenantId, GeneratedAt = DateTime.UtcNow } };
            });
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-05-01&through=2026-05-07");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        var toggle = Page.GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Auto-refresh every 30 seconds" });
        var outside = Page.GetByRole(AriaRole.Heading, new() { Name = "Financial Reports", Exact = true });
        await Expect(toggle).ToBeCheckedAsync();
        await toggle.ClickAsync();
        await outside.ClickAsync();
        await Expect(toggle).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        await Expect(toggle).Not.ToBeCheckedAsync();
        await outside.ClickAsync();
        await Expect(toggle).ToHaveCountAsync(0);
        var paused = Volatile.Read(ref calls);
        await Task.Delay(TimeSpan.FromSeconds(32));
        Volatile.Read(ref calls).Should().Be(paused);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        await Expect(toggle).Not.ToBeCheckedAsync();
        await toggle.ClickAsync();
        await outside.ClickAsync();
        await Expect(toggle).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        await Expect(toggle).ToBeCheckedAsync();
        await outside.ClickAsync();
        await Expect(toggle).ToHaveCountAsync(0);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        while (Volatile.Read(ref calls) == paused) await Task.Delay(100, timeout.Token);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeEnabledAsync();
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task SharedPdfExporter_PaginatesLongTables_EmbedsUnicodeFont_AndTreatsLabelsAsText()
    {
        await Page.GotoAsync(app.Urls.Single() + "/finance/reports");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF", Exact = true })).ToBeEnabledAsync();
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.EvaluateAsync("""
            async () => {
                const exporter = await import('./_content/XFramework.Portal.Shared/reports/report-export.js');
                await exporter.exportPdf({title:'Inventory Report', tenantLabel:'Caf\u00e9 \u00df <script>window.injected=true</script>', generatedAt:'2026-10-07 UTC', scope:'All products',
                    charts:[{title:'Stock quantities',labels:['A','B'],values:[-5,20]}],
                    sections:[{heading:'Stock',columns:['Product','Quantity'],rows:Array.from({length:120},(_,i)=>['Caf\u00e9 product '+i,String(i)])}]});
            }
            """));
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "report-export-pagination.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); await download.SaveAsAsync(path);
        var pdf = System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(path));
        System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type /Page\b").Count.Should().BeGreaterThan(1);
        pdf.Should().Contain("NotoSans");
        (await Page.EvaluateAsync<bool>("window.injected === true")).Should().BeFalse();
        errors.Should().BeEmpty();
    }
}

[Route("/finance/reports")]
public sealed class FinancialReportFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html data-base-color='zinc' data-primary-color='blue' style='--radius:.5rem'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='portal-assets/css/app.css'><link rel='stylesheet' href='portal-assets/css/utilities.css'><link rel='stylesheet' href='XFramework.Portal.styles.css'><style>body{padding:16px;margin:0;font-family:Arial}</style></head><body>");
        b.OpenComponent<FinancialReportFixtureSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class FinancialReportFixtureSurface : ComponentBase
{
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public ThemeService Theme { get; set; } = null!;
    private bool showReport = true;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Theme.InitializeAsync();
    }
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        if (showReport)
        {
            b.OpenComponent<XFramework.Portal.Features.Finance.Pages.Reports>(0);
            b.CloseComponent();
        }
        b.OpenElement(1, "button");
        b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => showReport = !showReport));
        b.AddContent(3, showReport ? "Close report" : "Open report");
        b.CloseElement();
        b.OpenComponent<XfContainerPortalHost>(4); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(5); b.CloseComponent();
    }
}
