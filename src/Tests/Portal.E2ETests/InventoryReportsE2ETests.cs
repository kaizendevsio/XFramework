using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E"), Category("Module:Inventario"), Category("Area:Reports")]
public sealed class InventoryReportsE2ETests : PageTest
{
    private WebApplication app = null!;
    private InventoryReportFixtureState state = null!;
    private bool actualPdf;

    [OneTimeSetUp]
    public async Task Start()
    {
        state = new();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory,
                "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o =>
        {
            o.DetailedErrors = true;
            o.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero;
        });
        builder.Services.AddAuthentication("ReportFixture")
            .AddScheme<AuthenticationSchemeOptions, InventoryReportFixtureAuth>("ReportFixture", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(state);
        state.Tenant.SetupGet(x => x.SelectedTenantId).Returns(() => state.SelectedTenant);
        state.Tenant.SetupGet(x => x.SelectedTenantName).Returns(() => state.SelectedTenant == state.FirstTenant ? "Fixture North" : "Fixture South");
        builder.Services.AddSingleton(state.Tenant.Object);
        var module = new Mock<IPortalModuleAvailability>();
        module.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        module.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        builder.Services.AddSingleton(module.Object);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetInventoryReportSnapshot(It.IsAny<GetInventoryReportSnapshotRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetInventoryReportSnapshotRequest request, CancellationToken ct) => state.Load(request, ct));
        builder.Services.AddSingleton(wrapper.Object);
        app = builder.Build();
        app.UseStaticFiles();
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var configuration = new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent!.Name;
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root!.FullName, "src", "Presentation",
                "XFramework.Portal.Features.Inventario", "obj", configuration, "net10.0", "scopedcss", "projectbundle")),
            RequestPath = "/_content/XFramework.Portal.Features.Inventario"
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src", "Presentation", "XFramework.Portal", "wwwroot", "css")),
            RequestPath = "/inventory-fixture-css"
        });
        var pdfRoot = Environment.GetEnvironmentVariable("INVENTORY_REPORT_PDF_ROOT") ?? Path.Combine(root.FullName,
            "src", "Presentation", "XFramework.Portal.Shared", "wwwroot");
        actualPdf = File.Exists(Path.Combine(pdfRoot, "reports", "report-export.js"));
        if (actualPdf) app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(pdfRoot), RequestPath = "/inventory-fixture-pdf"
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        // Capture the exact model and use parent-owned assets when available after integration.
        app.MapGet("/_content/XFramework.Portal.Shared/reports/report-export.js", () =>
            Microsoft.AspNetCore.Http.Results.Text(actualPdf
                ? "import {exportPdf as realExportPdf} from '/inventory-fixture-pdf/reports/report-export.js'; export function exportPdf(model){window.inventoryPdfSnapshot=model;return realExportPdf(model);}"
                : "export function exportPdf(model){window.inventoryPdfSnapshot=model;}", "text/javascript"));
        app.MapRazorComponents<InventoryReportFixtureRoot>().AddInteractiveServerRenderMode().RequireAuthorization();
        await app.StartAsync();
        TestContext.Progress.WriteLine("Synthetic inventory report host: " + app.Urls.Single());
    }

    [OneTimeTearDown]
    public async Task Stop() { await app.StopAsync(); await app.DisposeAsync(); }

    [SetUp]
    public async Task Load()
    {
        state.Reset();
        await Context.AddCookiesAsync([new Microsoft.Playwright.Cookie { Name = "report-fixture-auth", Value = "1", Url = app.Urls.Single() }]);
        await Page.GotoAsync(app.Urls.Single() + "/inventario/reports");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [TestCase(390, 844, false)]
    [TestCase(1366, 768, false)]
    [TestCase(390, 844, true)]
    [TestCase(1366, 768, true)]
    public async Task Reports_ChartsGridsShareAndPdf_UseSameSnapshotWithoutWrites(int width, int height, bool dark)
    {
        await Page.SetViewportSizeAsync(width, height);
        if (dark) await Page.EvaluateAsync("document.documentElement.classList.add('dark')");
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('[data-slot=bar-chart] svg').length >= 2");
        (await Page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('[data-slot=bar-chart] svg')].every(c => {
                const r=c.getBoundingClientRect();
                return r.width>100 && r.height>100 && [...c.querySelectorAll('path[fill]')].some(p=>p.getAttribute('fill')!=='none'&&p.getBBox().height>10);
            })
            """)).Should().BeTrue("the pinned native charts use SVG and must render nonzero bars");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"))
            .Should().BeTrue("the report must not overflow narrow viewports");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        var uri = await Page.GetByRole(AriaRole.Textbox, new() { Name = "Protected report link" }).InputValueAsync();
        uri.Should().Contain("tenant=" + state.FirstTenant).And.Contain("from=").And.Contain("to=").And.Contain("expiry=30");
        if (actualPdf)
        {
            var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot" }).ClickAsync());
            download.SuggestedFilename.Should().Be("Inventory-reports.pdf");
            var path = await download.PathAsync();
            var bytes = await File.ReadAllBytesAsync(path!);
            System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
            bytes.Length.Should().BeGreaterThan(2000);
        }
        else await Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot" }).ClickAsync();
        await Page.WaitForFunctionAsync("() => !!window.inventoryPdfSnapshot");
        (await Page.EvaluateAsync<string>("() => inventoryPdfSnapshot.tenantLabel")).Should().Be("Fixture North");
        (await Page.EvaluateAsync<int>("() => inventoryPdfSnapshot.charts.length")).Should().Be(2);
        (await Page.EvaluateAsync<double[]>("() => inventoryPdfSnapshot.charts[0].values")).Should().Equal(20, 5, 15);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Movements", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Receipt", new() { Exact = true }).First).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Expiry risks", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("EXP-1", new() { Exact = true })).ToBeVisibleAsync();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-reports");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"inventory-{width}-{(dark ? "dark" : "light")}.png"), FullPage = true });
        state.MaxActive.Should().Be(1);
        // Strict wrapper permits only report reads; any operational/financial call fails the test host.
    }

    [Test]
    public async Task Filters_Apply_RequeriesExplicitTenantAndPreservesReportDates()
    {
        var before = state.Requests.Last();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Product", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Fixture Widget", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Expiry window (days)" }).FillAsync("17");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/inventario/reports\\?"));
        await Wait(() => state.Requests.Last().ProductId == state.ProductId && state.Requests.Last().DaysAhead == 17);
        await Expect(Page.Locator(".snapshot-scope").First).ToContainTextAsync("Expiry 17 days");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true })).ToBeEnabledAsync();
        var request = state.Requests.Last();
        request.Metadata.RequestedTenantId.Should().Be(state.FirstTenant);
        request.ProductId.Should().Be(state.ProductId);
        request.DaysAhead.Should().Be(17);
        request.FromUtc.Should().Be(before.FromUtc);
        request.ToUtc.Should().Be(before.ToUtc);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Product", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Clear selection", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Wait(() => state.Requests.Last().ProductId is null);
        state.Requests.Last().DaysAhead.Should().Be(17);
    }

    [Test]
    public async Task ProtectedShare_OpenReauthorizesExplicitTenantWithoutSwitchingSelector()
    {
        var tenant = Guid.NewGuid();
        await Page.GotoAsync(app.Urls.Single() + $"/inventario/reports?tenant={tenant}&product={state.ProductId}&warehouse={state.WarehouseId}&location={state.LocationId}&from=2026-10-01&to=2026-10-07&expiry=17");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        var request = state.Requests.Last();
        request.Metadata.RequestedTenantId.Should().Be(tenant);
        request.ProductId.Should().Be(state.ProductId);
        request.WarehouseId.Should().Be(state.WarehouseId);
        request.LocationId.Should().Be(state.LocationId);
        request.DaysAhead.Should().Be(17);
        request.ToUtc.Should().Be(DateTime.SpecifyKind(new DateTime(2026, 10, 8).AddTicks(-1), DateTimeKind.Utc));
        state.SelectedTenant.Should().Be(state.FirstTenant);
        state.Status = HttpStatusCode.Forbidden;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Could not open this report");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot" })).ToBeDisabledAsync();
        await Context.ClearCookiesAsync();
        var response = await Page.GotoAsync(app.Urls.Single() + $"/inventario/reports?tenant={tenant}");
        response!.Status.Should().Be(401, "a share URL is not a bearer credential");
    }

    [Test]
    public async Task Refresh_FailureRetainsStableAsOf_AndLateTenantResponseIsDiscarded()
    {
        var asOf = await Page.Locator(".snapshot-scope").First.InnerTextAsync();
        state.Status = HttpStatusCode.ServiceUnavailable;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("last successful snapshot");
        (await Page.Locator(".snapshot-scope").First.InnerTextAsync()).Should().Be(asOf);
        state.Status = HttpStatusCode.OK;
        state.HoldNext = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Wait(() => Volatile.Read(ref state.Active) == 1);
        state.SelectedTenant = state.SecondTenant;
        state.Tenant.Raise(x => x.OnChanged += null);
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
        state.HoldNext.SetResult();
        await Expect(Page.GetByText("South Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
        state.MaxActive.Should().Be(1, "replacement reads wait for an ignored cancellation to finish");
        state.CanceledRequests.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Live_RefreshesAfterThirtySeconds_AndStopsWhenDisabled()
    {
        var initial = state.Requests.Count;
        await Page.GetByRole(AriaRole.Switch, new() { Name = "Live (30 seconds)" }).ClickAsync();
        await Wait(() => state.Requests.Count > initial, TimeSpan.FromSeconds(38));
        await Page.GetByRole(AriaRole.Switch, new() { Name = "Live (30 seconds)" }).ClickAsync();
        var after = state.Requests.Count;
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Should().HaveCount(after);
        state.MaxActive.Should().Be(1);
    }

    private static async Task Wait(Func<bool> predicate, TimeSpan? timeout = null)
    {
        using var ct = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(25, ct.Token);
    }
}

public sealed class InventoryReportFixtureState
{
    public Guid FirstTenant { get; } = Guid.NewGuid();
    public Guid SecondTenant { get; } = Guid.NewGuid();
    public Guid ProductId { get; } = Guid.NewGuid();
    public Guid WarehouseId { get; } = Guid.NewGuid();
    public Guid LocationId { get; } = Guid.NewGuid();
    public Guid SelectedTenant;
    public Mock<IPortalTenantContext> Tenant { get; } = new();
    public ConcurrentQueue<GetInventoryReportSnapshotRequest> Requests { get; } = new();
    public HttpStatusCode Status = HttpStatusCode.OK;
    public TaskCompletionSource? HoldNext;
    public int Active, MaxActive, CanceledRequests;
    public void Reset()
    {
        SelectedTenant = FirstTenant; Status = HttpStatusCode.OK; HoldNext = null;
        Active = 0; MaxActive = 0; CanceledRequests = 0; Requests.Clear();
    }
    public async Task<QueryResponse<InventoryReportSnapshot>> Load(GetInventoryReportSnapshotRequest request, CancellationToken ct)
    {
        Requests.Enqueue(request);
        var active = Interlocked.Increment(ref Active);
        MaxActive = Math.Max(MaxActive, active);
        var tenant = request.Metadata.RequestedTenantId!.Value;
        var hold = HoldNext;
        try
        {
            if (hold is not null) await hold.Task; // Deliberately ignore cancellation to exercise stale-result protection.
            if (ct.IsCancellationRequested) Interlocked.Increment(ref CanceledRequests);
            var name = tenant == SecondTenant ? "South Widget" : "Fixture Widget";
            return new()
            {
                HttpStatusCode = Status,
                Response = Status != HttpStatusCode.OK ? null : new(tenant, DateTime.UtcNow, 20, 5, 15, 30, 10,
                    1, 1, true,
                    [new(Guid.NewGuid(), ProductId, name, null, null, null, WarehouseId, "Main warehouse", LocationId,
                        "Shelf A", null, null, 20, 5, 15)],
                    [new(Guid.NewGuid(), ProductId, name, null, null, null, WarehouseId, "Main warehouse", LocationId,
                        "Shelf A", null, null, InventoryMovementType.Receipt, 30, "Receipt", null, request.FromUtc.AddDays(1))],
                    [new(Guid.NewGuid(), "EXP-1", ProductId, name, null, null, null, WarehouseId, "Main warehouse",
                        LocationId, "Shelf A", 20, 15, DateTime.UtcNow.AddDays(5), InventoryLotStatus.Available)], [],
                    [new(ProductId, name)], [new(WarehouseId, "Main warehouse")], [new(LocationId, "Shelf A", WarehouseId)])
            };
        }
        finally { Interlocked.Decrement(ref Active); if (ReferenceEquals(HoldNext, hold)) HoldNext = null; }
    }
}

public sealed class InventoryReportFixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        Request.Cookies["report-fixture-auth"] == "1"
            ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "synthetic-report-reader")], Scheme.Name)), Scheme.Name))
            : AuthenticateResult.NoResult());
}

[Route("/inventario/reports")]
public sealed class InventoryReportFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html data-base-color='zinc' data-primary-color='blue' style='--radius:.5rem'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='inventory-fixture-css/app.css'><link rel='stylesheet' href='_content/XFramework.Portal.Features.Inventario/XFramework.Portal.Features.Inventario.bundle.scp.css'><style>body{margin:16px;font-family:Arial}button{gap:8px}</style></head><body>");
        b.OpenComponent<InventoryReportFixtureSurface>(1);
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false));
        b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class InventoryReportFixtureSurface : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Reports>(0); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(1); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(2); b.CloseComponent();
    }
}
