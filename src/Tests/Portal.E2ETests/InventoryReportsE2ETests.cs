using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Inventario.Integration.Drivers;
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
        builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
        {
            options.DefaultBaseColor = BaseColor.Slate;
            options.DefaultPrimaryColor = PrimaryColor.Blue;
            options.DefaultDarkMode = false;
            options.DetectSystemPreference = true;
            options.DefaultRadius = 0.5;
            options.PersistToLocalStorage = true;
        });
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(state);
        state.Tenant.SetupGet(x => x.SelectedTenantId).Returns(() => state.SelectedTenant);
        state.Tenant.SetupGet(x => x.SelectedTenantName).Returns(() => state.SelectedTenant == state.FirstTenant ? "Fixture North" : "Fixture South");
        builder.Services.AddSingleton(state.Tenant.Object);
        var module = new Mock<IPortalModuleAvailability>();
        module.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => state.EnsureAvailability(ct));
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
        var appSource = await File.ReadAllTextAsync(Path.Combine(root.FullName, "src", "Presentation", "XFramework.Portal", "Components", "App.razor"));
        var themeScript = System.Text.RegularExpressions.Regex.Match(appSource, "<script>(.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        themeScript.Should().Contain("bb-theme");
        app.MapGet("/inventory-fixture-theme.js", () => Microsoft.AspNetCore.Http.Results.Text(themeScript, "text/javascript"));
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
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Stock positions", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [TestCase(390, 844, false)]
    [TestCase(1920, 1080, false)]
    [TestCase(1024, 768, false)]
    [TestCase(390, 844, true)]
    [TestCase(1920, 1080, true)]
    [TestCase(1024, 768, true)]
    public async Task Reports_ChartsGridsShareAndPdf_UseSameSnapshotWithoutWrites(int width, int height, bool dark)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true }).ClickAsync();
        await Page.EvaluateAsync("dark => localStorage.setItem('bb-theme', JSON.stringify({ baseColor: 'slate', primaryColor: 'blue', radius: 0.5, isDarkMode: dark }))", dark);
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToHaveAttributeAsync("aria-selected", "true");
        await AssertTheme(dark);
        (await Page.APIRequest.GetAsync(app.Urls.Single() + "/inventory-fixture-css/utilities.css")).Ok.Should().BeTrue();
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('[data-slot=pie-chart] svg').length >= 2");
        (await Page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('[data-slot=pie-chart] svg')].every(c => {
                const r=c.getBoundingClientRect();
                return r.width>100 && r.height>100 && [...c.querySelectorAll('path[fill]')].some(p=>p.getAttribute('fill')!=='none'&&p.getBBox().height>10);
            })
            """)).Should().BeTrue("the native doughnut charts must render nonzero slices");
        await Page.WaitForFunctionAsync("() => [...document.querySelectorAll('[data-slot=line-chart] svg path')].some(p => p.getBBox().width > 100)");
        (await Page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('[data-slot=line-chart] svg text')].every(label => {
                const bounds = label.ownerSVGElement.getBoundingClientRect();
                const rect = label.getBoundingClientRect();
                return rect.left >= bounds.left - 1 && rect.right <= bounds.right + 1;
            })
            """)).Should().BeTrue("timeline labels must fit inside the chart");
        await Expect(Page.Locator("[data-slot=data-grid]")).ToHaveCountAsync(0);
        (await Page.Locator("body").InnerTextAsync()).Should().NotContain("As of ").And.NotContain("svg");
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
        (await Page.EvaluateAsync<double[]>("() => inventoryPdfSnapshot.charts[0].values")).Should().Equal(15, 5);
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-reports");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"summary-{width}-{(dark ? "dark" : "light")}.png"), FullPage = true });
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Movements", Exact = true }).ClickAsync();
        await Expect(Page.Locator("[data-slot=pie-chart], [data-slot=line-chart]")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Receipt", new() { Exact = true }).First).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Expiry risks", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("EXP-1", new() { Exact = true })).ToBeVisibleAsync();
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"inventory-{width}-{(dark ? "dark" : "light")}.png"), FullPage = true });
        await AssertTheme(dark);
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
        await Expect(Page.Locator(".snapshot-scope")).ToHaveCountAsync(0);
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

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public async Task Positions_NativePaginationAndFilters_RenderOnlyActiveView(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        state.PositionRows = 25;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".report-grid tbody tr")).ToHaveCountAsync(10);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Filter Product", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Fixture Widget 11", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Fixture Widget 01", new() { Exact = true })).ToBeHiddenAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Filter Product", Exact = true }).ClickAsync();
        await Page.GetByPlaceholder("Enter value...").FillAsync("Fixture Widget 25");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).Last.ClickAsync();
        await Expect(Page.Locator(".report-grid tbody tr").Filter(new() { HasText = "Fixture Widget" })).ToHaveCountAsync(1);
        await Expect(Page.GetByText("Fixture Widget 25", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".report-grid")).ToHaveCountAsync(0);
        await Page.GoBackAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Stock positions", Exact = true })).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.Locator("[data-slot=pie-chart], [data-slot=line-chart]")).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
    }

    [Test]
    public async Task ProtectedShare_OpenReauthorizesExplicitTenantWithoutSwitchingSelector()
    {
        var tenant = Guid.NewGuid();
        await Page.GotoAsync(app.Urls.Single() + $"/inventario/reports?tenant={tenant}&product={state.ProductId}&warehouse={state.WarehouseId}&location={state.LocationId}&from=2026-10-01&to=2026-10-07&expiry=17&view=positions");
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task Initialization_TenantChangesDuringAvailability_UsesCurrentScopeAndPreservesExplicitLinks(bool explicitTenant)
    {
        await Page.GotoAsync("about:blank");
        state.Requests.Clear();
        state.AvailabilityTenants.Clear();
        var availability = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.HoldAvailability = availability;
        state.AvailabilityEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await Page.GotoAsync(app.Urls.Single() + "/inventario/reports" +
                (explicitTenant ? $"?tenant={state.FirstTenant}" : ""));
            await state.AvailabilityEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            state.SelectedTenant = state.SecondTenant;
            state.Tenant.Raise(x => x.OnChanged += null);
            state.Requests.Should().BeEmpty("report reads must wait for availability initialization");
            availability.SetResult();

            await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Summary", Exact = true })).ToBeVisibleAsync();
            var expectedTenant = explicitTenant ? state.FirstTenant : state.SecondTenant;
            state.AvailabilityTenants.Should().ContainInOrder(state.FirstTenant, state.SecondTenant);
            state.Requests.Should().NotBeEmpty().And.OnlyContain(x => x.Metadata.RequestedTenantId == expectedTenant);
            state.SelectedTenant.Should().Be(state.SecondTenant, "shared links must not switch the tenant selector");
            await Page.GetByRole(AriaRole.Tab, new() { Name = "Stock positions", Exact = true }).ClickAsync();
            await Expect(Page.GetByText(explicitTenant ? "Fixture Widget" : "South Widget", new() { Exact = true }).First).ToBeVisibleAsync();
            await Expect(Page.GetByText(explicitTenant ? "South Widget" : "Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Textbox, new() { Name = "Protected report link" }))
                .ToHaveValueAsync(new System.Text.RegularExpressions.Regex($"[?&]tenant={expectedTenant}(?:&|$)"));
        }
        finally { availability.TrySetResult(); }
    }

    [Test]
    public async Task Refresh_FailureRetainsStableAsOf_AndLateTenantResponseIsDiscarded()
    {
        var originalSnapshot = state.LastGeneratedAt;
        state.Status = HttpStatusCode.ServiceUnavailable;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("last successful snapshot");
        await Page.GetByRole(AriaRole.Button, new() { Name = "PDF snapshot" }).ClickAsync();
        await Page.WaitForFunctionAsync("() => !!window.inventoryPdfSnapshot");
        DateTime.Parse(await Page.EvaluateAsync<string>("() => inventoryPdfSnapshot.generatedAt"),
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).Should().Be(originalSnapshot);
        state.Status = HttpStatusCode.OK;
        state.HoldNext = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Wait(() => Volatile.Read(ref state.Active) == 1);
        state.SelectedTenant = state.SecondTenant;
        await Page.EvaluateAsync("""
            url => {
                const link = document.createElement('a');
                link.href = url;
                document.body.append(link);
                link.click();
                link.remove();
            }
            """, app.Urls.Single() + $"/inventario/reports?tenant={state.SecondTenant}&view=positions");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
        state.HoldNext.SetResult();
        await Expect(Page.GetByText("South Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeHiddenAsync();
        state.MaxActive.Should().Be(1, "replacement reads wait for an ignored cancellation to finish");
        state.CanceledRequests.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Live_RepeatedEnableDisable_LeavesCircuitResponsiveAndDisposesSafely()
    {
        var errors = new ConcurrentQueue<string>();
        Page.PageError += (_, error) => errors.Enqueue(error);
        Page.Console += (_, message) => { if (message.Type == "error") errors.Enqueue(message.Text); };
        for (var i = 0; i < 3; i++)
        {
            await ToggleLive();
            await ToggleLive();
            var before = state.Requests.Count;
            await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
            await Wait(() => state.Requests.Count > before);
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })).ToBeEnabledAsync();
        }
        await Page.GotoAsync(app.Urls.Single() + $"/inventario/reports?tenant={state.FirstTenant}&view=positions");
        await Expect(Page.GetByText("Fixture Widget", new() { Exact = true }).First).ToBeVisibleAsync();
        errors.Should().BeEmpty("toggling or disposing the live loop must not terminate the circuit");
        state.MaxActive.Should().Be(1);
    }

    [Test]
    public async Task Live_RefreshesAfterThirtySeconds_AndStopsWhenDisabled()
    {
        var initial = state.Requests.Count;
        await ToggleLive();
        await Wait(() => state.Requests.Count > initial, TimeSpan.FromSeconds(38));
        await ToggleLive();
        var after = state.Requests.Count;
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Should().HaveCount(after);
        state.MaxActive.Should().Be(1);
    }

    [Test]
    public async Task Live_HiddenPageSkipsRefresh_AndDisposalStopsTimer()
    {
        await Page.EvaluateAsync("Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' })");
        await ToggleLive();
        var before = state.Requests.Count;
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Should().HaveCount(before);
        await Page.EvaluateAsync("Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' })");
        await Wait(() => state.Requests.Count > before, TimeSpan.FromSeconds(38));
        await Page.GotoAsync("about:blank");
        var after = state.Requests.Count;
        await Page.WaitForTimeoutAsync(31000);
        state.Requests.Should().HaveCount(after);
    }

    private static async Task Wait(Func<bool> predicate, TimeSpan? timeout = null)
    {
        using var ct = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(25, ct.Token);
    }

    private async Task ToggleLive()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitemcheckbox, new() { Name = "Auto-refresh every 30 seconds" }).ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task Refresh_SplitButtonIsJoined_AndMenuDismissesOutside()
    {
        var group = Page.GetByRole(AriaRole.Group, new() { Name = "Refresh controls" });
        var refresh = group.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });
        var options = group.GetByRole(AriaRole.Button, new() { Name = "Refresh options", Exact = true });
        var left = (await refresh.BoundingBoxAsync())!;
        var right = (await options.BoundingBoxAsync())!;
        Math.Abs(right.X - (left.X + left.Width)).Should().BeLessThanOrEqualTo(1);
        await options.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitemcheckbox)).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Inventory Reports", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitemcheckbox)).ToBeHiddenAsync();
    }

    private async Task AssertTheme(bool dark)
    {
        (await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('dark')")).Should().Be(dark);
        (await Page.Locator("html").GetAttributeAsync("data-base-color")).Should().Be("slate");
        (await Page.Locator("html").GetAttributeAsync("data-primary-color")).Should().Be("blue");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.documentElement).getPropertyValue('--radius').trim()")).Should().Be("0.5rem");
        var brightness = await Page.EvaluateAsync<double>("""
            () => {
                const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
                const ctx = canvas.getContext('2d');
                ctx.fillStyle = getComputedStyle(document.body).backgroundColor;
                ctx.fillRect(0, 0, 1, 1);
                const [r,g,b] = ctx.getImageData(0, 0, 1, 1).data;
                return (r+g+b)/765;
            }
            """);
        if (dark) brightness.Should().BeLessThan(.25, "the rendered background must actually be dark");
        else brightness.Should().BeGreaterThan(.75, "the rendered background must actually be light");
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
    public TaskCompletionSource? HoldAvailability, AvailabilityEntered;
    public ConcurrentQueue<Guid> AvailabilityTenants { get; } = new();
    public int Active, MaxActive, CanceledRequests;
    public int PositionRows = 1;
    public DateTime LastGeneratedAt;
    public void Reset()
    {
        SelectedTenant = FirstTenant; Status = HttpStatusCode.OK; HoldNext = null;
        HoldAvailability = null; AvailabilityEntered = null; AvailabilityTenants.Clear();
        Active = 0; MaxActive = 0; CanceledRequests = 0; PositionRows = 1; Requests.Clear();
    }
    public async Task EnsureAvailability(CancellationToken ct)
    {
        AvailabilityTenants.Enqueue(SelectedTenant);
        var hold = Interlocked.Exchange(ref HoldAvailability, null);
        if (hold is null) return;
        AvailabilityEntered?.TrySetResult();
        await hold.Task.WaitAsync(ct);
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
            if (Status == HttpStatusCode.OK) LastGeneratedAt = DateTime.UtcNow;
            return new()
            {
                HttpStatusCode = Status,
                Response = Status != HttpStatusCode.OK ? null : new(tenant, LastGeneratedAt, 20, 5, 15, 30, 10,
                    PositionRows, 1, true,
                    Enumerable.Range(1, PositionRows).Select(i => new StockPositionReportRow(Guid.NewGuid(), ProductId,
                        PositionRows == 1 ? name : $"{name} {i:00}", null, null, null, WarehouseId, "Main warehouse", LocationId,
                        "Shelf A", null, null, 20, 5, 15)).ToList(),
                    [new(Guid.NewGuid(), ProductId, name, null, null, null, WarehouseId, "Main warehouse", LocationId,
                        "Shelf A", null, null, InventoryMovementType.Receipt, 30, "Receipt", null, request.FromUtc.AddDays(1))],
                    [new(Guid.NewGuid(), "EXP-1", ProductId, name, null, null, null, WarehouseId, "Main warehouse",
                        LocationId, "Shelf A", 20, 15, DateTime.UtcNow.AddDays(5), InventoryLotStatus.Available)], [],
                    [new(ProductId, name)], [new(WarehouseId, "Main warehouse")], [new(LocationId, "Shelf A", WarehouseId)])
                {
                    MovementTimeline = Enumerable.Range(0, (request.ToUtc.Date - request.FromUtc.Date).Days + 1)
                        .Select(i => new InventoryMovementDailyTotal(request.FromUtc.Date.AddDays(i),
                            i == 1 ? 20 : i == 2 ? 10 : 0, i == 2 ? 10 : 0)).ToList()
                }
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
        b.AddMarkupContent(0, "<!doctype html><html lang='en'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='inventory-fixture-css/app.css'><link rel='stylesheet' href='inventory-fixture-css/utilities.css'><link rel='stylesheet' href='_content/XFramework.Portal.Features.Inventario/XFramework.Portal.Features.Inventario.bundle.scp.css'><script src='inventory-fixture-theme.js'></script><style>body{margin:16px;background:var(--background);color:var(--foreground)}</style></head><body class='font-sans antialiased'>");
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
        b.OpenComponent<Router>(0);
        b.AddAttribute(1, "AppAssembly", typeof(XFramework.Portal.Features.Inventario.Pages.Reports).Assembly);
        b.AddAttribute(2, "Found", (RenderFragment<RouteData>)(route => content =>
        {
            content.OpenComponent<RouteView>(0);
            content.AddAttribute(1, "RouteData", route);
            content.CloseComponent();
        }));
        b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(3); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(4); b.CloseComponent();
    }
}
