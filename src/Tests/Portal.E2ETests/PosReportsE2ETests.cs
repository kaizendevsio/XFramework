using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E"), Category("Module:POS"), Category("Area:Reports")]
public sealed class PosReportsE2ETests : PageTest
{
    private WebApplication app = null!;
    private PosReportBrowserState state = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        state = new();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => { o.DetailedErrors = true; o.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero; });
        builder.Services.AddAuthentication("PosReportFixture").AddScheme<AuthenticationSchemeOptions, PosReportBrowserAuth>("PosReportFixture", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        state.Tenant.SetupGet(x => x.SelectedTenantId).Returns(() => state.SelectedTenant);
        state.Tenant.SetupGet(x => x.SelectedTenantName).Returns(() => state.SelectedTenant == state.FirstTenant ? "POS North" : "POS South");
        state.Modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        state.Modules.Setup(x => x.IsFeatureEnabled("pos", "reporting")).Returns(() => state.Enabled);
        state.Modules.Setup(x => x.IsFeatureEnabled("wallets", null)).Returns(false);
        builder.Services.AddSingleton(state.Tenant.Object);
        builder.Services.AddSingleton(state.Modules.Object);
        // Optional register catalogs deny access. Currency labels must come from the POS snapshot, not Wallets reads.
        builder.Services.AddSingleton(state.Data.Object);
        var wrapper = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetPosSalesReport(It.IsAny<GetPosSalesReportRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetPosSalesReportRequest request, CancellationToken ct) => state.Load(request, ct));
        builder.Services.AddSingleton(wrapper.Object);
        app = builder.Build();
        app.UseStaticFiles();
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var presentation = Path.Combine(root!.FullName, "src", "Presentation");
        var configuration = new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent!.Name;
        void Assets(string path, string requestPath) => app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(path), RequestPath = requestPath });
        Assets(Path.Combine(presentation, "XFramework.Portal", "wwwroot"), "/pos-fixture-assets");
        Assets(Path.Combine(presentation, "XFramework.Portal.Shared", "wwwroot"), "/pos-fixture-shared");
        Assets(Path.Combine(presentation, "XFramework.Portal.Features.POS", "wwwroot"), "/_content/XFramework.Portal.Features.POS");
        Assets(Path.Combine(presentation, "XFramework.Portal.Features.POS", "obj", configuration, "net10.0", "scopedcss", "projectbundle"), "/_content/XFramework.Portal.Features.POS");
        app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        app.MapGet("/_content/XFramework.Portal.Shared/reports/report-export.js", () => Microsoft.AspNetCore.Http.Results.Text(
            "import { exportPdf as realExport } from '/pos-fixture-shared/reports/report-export.js'; export { copyReportLink } from '/pos-fixture-shared/reports/report-export.js'; export function exportPdf(model){window.posPdf=model;return realExport(model);}", "text/javascript"));
        app.MapRazorComponents<PosReportBrowserRoot>().AddInteractiveServerRenderMode().RequireAuthorization();
        await app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { await app.StopAsync(); await app.DisposeAsync(); }

    [SetUp]
    public async Task Load()
    {
        state.Reset();
        await Context.AddCookiesAsync([new Microsoft.Playwright.Cookie { Name = "pos-report-auth", Value = "1", Url = app.Urls.Single() }]);
        await Page.GotoAsync(app.Urls.Single() + "/pos/reports");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot", Exact = true })).ToBeEnabledAsync();
    }

    [TearDown]
    public async Task CaptureFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            await Capture("failure-" + TestContext.CurrentContext.Test.ID);
    }

    [TestCase(390, 844)]
    [TestCase(1920, 1080)]
    public async Task Reports_RenderCurrencySeparatedNetCharts_PagesAndRealPdf(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Expect(Page.Locator(".xf-summary-grid").GetByText("Net sales", new() { Exact = true })).ToHaveCountAsync(2);
        await Expect(Page.Locator(".currency-section h4")).ToHaveTextAsync(["USD", "EUR"]);
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('[data-slot=pie-chart] svg').length === 2");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Receipts: page 2", Exact = true })).ToBeVisibleAsync();
        state.Requests.Last().Page.Should().Be(2);
        await Expect(Page.Locator(".receipt-grid table:visible")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".receipt-grid table:visible tbody tr")).ToHaveCountAsync(25);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Successful refunds: page 1", Exact = true })).Not.ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Refunds (27)", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Receipts: page 2", Exact = true })).Not.ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next refunds", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Successful refunds: page 2", Exact = true })).ToBeVisibleAsync();
        state.Requests.Last().RefundPage.Should().Be(2);
        state.Requests.Last().Page.Should().Be(2);
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Refunds (27)", Exact = true })).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.Locator(".receipt-grid table:visible")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".receipt-grid table:visible tbody tr")).ToHaveCountAsync(2);
        await Capture($"refunds-{width}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts (51)", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Receipts: page 2", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator(".receipt-grid table:visible")).ToHaveCountAsync(1);
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot", Exact = true }).ClickAsync());
        var bytes = await File.ReadAllBytesAsync((await download.PathAsync())!);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
        (await Page.EvaluateAsync<string>("() => posPdf.sections[0].rows[0][4]")).Should().Be("75.00");
        (await Page.EvaluateAsync<int>("() => posPdf.charts.length")).Should().Be(2);
        var pdf = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "pos-reports", $"report-{width}.pdf");
        await download.SaveAsAsync(pdf);
        TestContext.AddTestAttachment(pdf);
        await Capture($"reports-{width}");
    }

    [Test]
    public async Task Share_OpensCopyDialog_AndDateLabelIsAbovePicker()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        var label = await Page.Locator(".date-filter label").BoundingBoxAsync();
        var picker = await Page.Locator(".date-filter button").First.BoundingBoxAsync();
        label.Should().NotBeNull();
        picker.Should().NotBeNull();
        (label!.Y + label.Height).Should().BeLessThanOrEqualTo(picker!.Y + 1);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "Share report" })).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Textbox, new() { Name = "Protected report link" })).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("/pos/reports\\?tenant="));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Copy link", Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Copied", Exact = true })).ToBeVisibleAsync();
        await Capture("share-dialog-1920");
    }

    [Test]
    public async Task Overview_IsConcise_PresetsChangeRange_AndDonutDrillsIntoReports()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync(app.Urls.Single() + "/pos/overview");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "POS Overview", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-slot=pie-chart] svg").First).ToBeVisibleAsync();
        await Expect(Page.Locator(".currency-section h4")).ToHaveTextAsync(["USD", "EUR"]);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.Locator(".receipt-grid table")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Receipts in", Exact = false }).First).ToBeEnabledAsync();
        state.Requests.Last().FromUtc.Should().Be(DateTime.UtcNow.Date);
        await Page.GetByRole(AriaRole.Button, new() { Name = "This week", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })).ToBeEnabledAsync();
        state.Requests.Last().FromUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
        await Page.GetByRole(AriaRole.Button, new() { Name = "This month", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })).ToBeEnabledAsync();
        state.Requests.Last().FromUtc.Day.Should().Be(1);
        await Capture("overview-1920");
        var donut = Page.Locator("[data-slot=pie-chart]").First;
        await donut.ScrollIntoViewIfNeededAsync();
        var point = await donut.EvaluateAsync<float[]>("""
            element => {
                for (const path of element.querySelectorAll('svg path[fill]')) {
                    if (path.getAttribute('fill') === 'none') continue;
                    const r = path.getBoundingClientRect();
                    if (r.width < 20 || r.height < 20) continue;
                    for (let x = r.left + 8; x < r.right - 8; x += 8)
                        for (let y = r.top + 8; y < r.bottom - 8; y += 8)
                            if (document.elementFromPoint(x, y) === path) return [x, y];
                }
                throw new Error('No rendered donut segment');
            }
            """);
        await Page.Mouse.ClickAsync(point[0], point[1]);
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/pos/reports\\?.*payment="));
        await Expect(Page.Locator(".receipt-grid table").First).ToBeVisibleAsync();
        state.Requests.Last().CurrencyId.Should().Be(state.Currency);
        state.Requests.Last().PaymentMethod.Should().NotBeNull();
        state.Data.Verify(x => x.Query<CurrencyType>(), Times.Never);
    }

    [Test]
    public async Task MissingCurrencyLabels_StaySeparatedWithoutGuidHeadings()
    {
        state.IncludeLabels = false;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Unknown currency 1", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Unknown currency 2", Exact = true })).ToBeVisibleAsync();
        (await Page.Locator(".pos-report").InnerTextAsync()).Should()
            .NotContain(state.Currency.ToString()[..8]).And.NotContain(state.OtherCurrency.ToString()[..8]);
        state.Data.Verify(x => x.Query<CurrencyType>(), Times.Never);
    }

    [Test]
    public async Task Overview_WalletsDisabled_UsesSnapshotCurrencyLabels()
    {
        await Page.GotoAsync(app.Urls.Single() + "/pos/overview");
        await Expect(Page.Locator(".currency-section h4")).ToHaveTextAsync(["USD", "EUR"]);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Receipts in USD", Exact = true })).ToBeEnabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Receipts in EUR", Exact = true })).ToBeEnabledAsync();
        state.Modules.Object.IsFeatureEnabled("wallets").Should().BeFalse();
        state.Data.Verify(x => x.Query<CurrencyType>(), Times.Never);
        (await Page.Locator(".pos-report").InnerTextAsync()).Should().NotContain("Unknown currency").And.NotContain("Unlabelled currency");
        await Page.SetViewportSizeAsync(1920, 1080);
        await Capture("overview-currencies-1920");
    }

    [Test]
    public async Task Overview_CurrencyDrilldown_IsKeyboardAccessible()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GotoAsync(app.Urls.Single() + "/pos/overview");
        var drilldown = Page.GetByRole(AriaRole.Button, new() { Name = "Receipts in", Exact = false }).First;
        await Expect(drilldown).ToBeEnabledAsync();
        await Capture("overview-390");
        await drilldown.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/pos/reports\\?.*currency="));
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts (51)", Exact = true })).ToBeVisibleAsync();
        state.Requests.Last().CurrencyId.Should().Be(state.Currency);
    }

    [Test]
    public async Task LinePoint_DrillsDateAndCurrency_PreservingTenant()
    {
        await Page.GotoAsync(app.Urls.Single() + $"/pos/reports?tenant={state.FirstTenant}&from=2026-09-01&to=2026-09-07");
        var chart = Page.Locator("[data-slot=line-chart]").First;
        await Expect(chart.Locator("svg")).ToBeVisibleAsync();
        await chart.ScrollIntoViewIfNeededAsync();
        var pointIndex = await chart.EvaluateAsync<int>("""
            element => {
                const paths = [...element.querySelectorAll("svg path")];
                const symbol = paths.filter(p => {
                    const r = p.getBoundingClientRect();
                    return (p.getAttribute('fill') === '#16845b' || p.getAttribute('stroke') === '#16845b') && r.width > 0 && r.width < 16 && r.height > 0 && r.height < 16;
                }).sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top)[0];
                return paths.indexOf(symbol);
            }
            """);
        // Locator clicks wait for chart animation and scrolling to settle before hitting the point.
        pointIndex.Should().BeGreaterThanOrEqualTo(0);
        await chart.Locator("svg path").Nth(pointIndex).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("currency="));
        await Expect(Page.Locator(".report-caption").Filter(new() { HasText = "2026-09-01 through 2026-09-01 UTC." })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot", Exact = true })).ToBeEnabledAsync();
        var request = state.Requests.Last();
        (request.ToUtcExclusive - request.FromUtc).Should().Be(TimeSpan.FromDays(1));
        request.Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        request.CurrencyId.Should().Be(state.Currency);
    }

    [Test]
    public async Task SharedLink_ReauthorizesTenantAndLogin_AndFeatureRevocationClearsData()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        var shared = await Page.GetByRole(AriaRole.Textbox, new() { Name = "Protected report link" }).InputValueAsync();
        shared.Should().Contain("tenant=" + state.FirstTenant).And.NotContain("token=");
        state.Enabled = false;
        state.Modules.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByText("POS Reporting is not enabled for this tenant.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-slot=pie-chart]")).ToHaveCountAsync(0);
        state.Enabled = true; state.Status = HttpStatusCode.Forbidden;
        await Page.GotoAsync(shared.Replace(state.FirstTenant.ToString(), state.SecondTenant.ToString()));
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(Page.Locator(".receipt-grid table")).ToHaveCountAsync(0);
        state.Requests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        await Context.ClearCookiesAsync();
        var response = await Page.GotoAsync(shared);
        response!.Status.Should().Be(401);
    }

    [Test]
    public async Task TenantChange_DiscardsLateResponseAndKeepsRequestsSerial()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.HoldNext = hold;
        var before = state.Requests.Count;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            while (state.Requests.Count == before) await Task.Delay(25, timeout.Token);
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        try { await Expect(Page.Locator("[data-slot=pie-chart]")).ToHaveCountAsync(0); }
        finally { hold.TrySetResult(); }
        await Expect(Page.GetByText("POS South", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot", Exact = true })).ToBeEnabledAsync();
        state.Requests.Last().Metadata.RequestedTenantId.Should().Be(state.SecondTenant);
        state.MaxActive.Should().Be(1);
    }

    [Test, Category("Slow")]
    public async Task AutoRefresh_UsesMenu_SkipsHiddenPages_StopsOnDisable()
    {
        await ToggleLive();
        var before = state.Requests.Count;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(38)))
            while (state.Requests.Count == before) await Task.Delay(50, timeout.Token);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })).ToBeEnabledAsync();
        await Page.EvaluateAsync("Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' })");
        before = state.Requests.Count;
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Count.Should().Be(before);
        await ToggleLive();
        await Page.EvaluateAsync("Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' })");
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Count.Should().Be(before);
        state.MaxActive.Should().Be(1);
    }
    private async Task ToggleLive()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Auto-refresh every 30 seconds" }).ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
    }

    private async Task Capture(string name)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "pos-reports");
        Directory.CreateDirectory(directory);
        var screenshot = Path.Combine(directory, name + ".png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        TestContext.AddTestAttachment(screenshot);
    }
}

public sealed class PosReportBrowserState
{
    public Guid FirstTenant { get; } = Guid.NewGuid();
    public Guid SecondTenant { get; } = Guid.NewGuid();
    public Guid Currency { get; } = Guid.NewGuid();
    public Guid OtherCurrency { get; } = Guid.NewGuid();
    public Guid Register { get; } = Guid.NewGuid();
    public Guid SelectedTenant;
    public bool Enabled = true;
    public bool IncludeLabels = true;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public Mock<IPortalTenantContext> Tenant { get; } = new();
    public Mock<IPortalModuleAvailability> Modules { get; } = new();
    public Mock<IDataContext> Data { get; } = new(MockBehavior.Strict);
    public ConcurrentQueue<GetPosSalesReportRequest> Requests { get; } = new();
    public int Active, MaxActive;
    public TaskCompletionSource? HoldNext;
    public void Reset() { SelectedTenant = FirstTenant; Enabled = true; IncludeLabels = true; Status = HttpStatusCode.OK; Requests.Clear(); Data.Invocations.Clear(); Active = MaxActive = 0; HoldNext = null; }
    public async Task<QueryResponse<PosSalesReportResponse>> Load(GetPosSalesReportRequest request, CancellationToken ct)
    {
        Requests.Enqueue(request);
        MaxActive = Math.Max(MaxActive, Interlocked.Increment(ref Active));
        try
        {
            var hold = Interlocked.Exchange(ref HoldNext, null);
            if (hold is not null) await hold.Task;
            if (Status != HttpStatusCode.OK) return new() { HttpStatusCode = Status };
            var currencies = new List<PosReportCurrencySummary>
            {
                new(Currency, 4, 100, 0, 0, 100, 60, 40) { RefundCount = 1, RefundTotal = 25 },
                new(OtherCurrency, 2, 200, 0, 0, 200, 150, 50) { RefundCount = 1, RefundTotal = 10 }
            }.Where(x => request.CurrencyId is null || x.CurrencyId == request.CurrencyId).ToList();
            var rows = Enumerable.Range(1, 51).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                .Select(i => new PosReportSaleRow(Guid.NewGuid(), $"POS-{i:000}", Register, "Test register", request.CurrencyId ?? Currency,
                    PosSaleStatus.Completed, request.PaymentMethod ?? PosPaymentMethod.CashDrawer, i,
                    request.FromUtc, request.FromUtc)).ToList();
            var refunds = Enumerable.Range(1, 27).Skip((request.RefundPage - 1) * request.PageSize).Take(request.PageSize)
                .Select(i => new PosReportRefundRow(Guid.NewGuid(), $"RETURN-{i:000}", Guid.NewGuid(), request.CurrencyId ?? Currency,
                    PosPaymentMethod.CashDrawer, i, request.FromUtc)).ToList();
            return new()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new(request.Metadata.RequestedTenantId!.Value, DateTime.UtcNow, request.FromUtc, request.ToUtcExclusive,
                    51, request.Page, request.PageSize, currencies,
                    currencies.Select(x => new PosReportDay(x.CurrencyId, request.FromUtc.AddHours(request.HourUtc ?? 0), x.CompletedCount, x.Total) { RefundTotal = x.RefundTotal }).ToList(),
                    [new(PosSaleStatus.Completed, 51)], rows)
                {
                    RefundCount = 27, RefundPage = request.RefundPage, RefundRows = refunds,
                    CurrencyLabels = IncludeLabels ? currencies.Select(x => new PosReportCurrencyLabel(x.CurrencyId, x.CurrencyId == Currency ? "USD" : "EUR")).ToList() : []
                }
            };
        }
        finally { Interlocked.Decrement(ref Active); }
    }
}

public sealed class PosReportBrowserAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Cookies["pos-report-auth"] == "1"
        ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "pos-report-reader")], Scheme.Name)), Scheme.Name))
        : AuthenticateResult.NoResult());
}

[Route("/pos/reports"), Route("/pos/overview")]
public sealed class PosReportBrowserRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html lang='en'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='pos-fixture-assets/css/app.css'><link rel='stylesheet' href='pos-fixture-assets/css/utilities.css'><link rel='stylesheet' href='_content/XFramework.Portal.Features.POS/XFramework.Portal.Features.POS.bundle.scp.css'><style>body{margin:16px;background:var(--background);color:var(--foreground)}</style></head><body>");
        b.OpenComponent<PosReportBrowserSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class PosReportBrowserSurface : ComponentBase
{
    [Inject] public ThemeService Theme { get; set; } = null!;
    protected override async Task OnAfterRenderAsync(bool firstRender) { if (firstRender) await Theme.InitializeAsync(); }
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<Router>(0);
        b.AddAttribute(1, "AppAssembly", typeof(XFramework.Portal.Features.POS.Pages.Reports).Assembly);
        b.AddAttribute(2, "Found", (RenderFragment<RouteData>)(route => content =>
        { content.OpenComponent<RouteView>(0); content.AddAttribute(1, "RouteData", route); content.CloseComponent(); }));
        b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(3); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(4); b.CloseComponent();
    }
}
