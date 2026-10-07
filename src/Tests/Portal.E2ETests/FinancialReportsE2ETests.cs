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
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(x => x.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(x => x.SelectedTenantId == tenantId && x.SelectedTenantName == "Report fixture"));
        var modules = new Mock<IPortalModuleAvailability>();
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
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal/wwwroot")), RequestPath = "/portal-assets" });
        app.UseAntiforgery();
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
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Daily activity - EUR" })).ToBeVisibleAsync();
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
        await Page.ScreenshotAsync(new() { Path = Path.ChangeExtension(pdfPath, ".png"), FullPage = true });
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
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

    [Test]
    public async Task FiltersAndShareLink_PreserveAppliedUtcRange_AndCalendarRemainsResponsive()
    {
        await Page.GotoAsync(app.Urls.Single() + $"/finance/reports?tenant={tenantId}&from=2026-09-01&through=2026-09-07");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Current balances" })).ToBeVisibleAsync();
        var dates = Page.Locator(".financial-report-filter input");
        await dates.Nth(0).FillAsync("2026-09-03"); await dates.Nth(0).PressAsync("Tab");
        await dates.Nth(1).FillAsync("2026-09-05"); await dates.Nth(1).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Activity: 2026-09-03 to 2026-09-05 (UTC)" })).ToBeVisibleAsync();
        latest!.From.Should().Be(new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        latest.ToExclusive.Should().Be(new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        await Page.EvaluateAsync("navigator.clipboard.writeText = async value => { window.sharedReportUrl = value; }");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Share link", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("window.sharedReportUrl !== undefined");
        (await Page.EvaluateAsync<string>("window.sharedReportUrl")).Should().EndWith($"finance/reports?tenant={tenantId}&from=2026-09-03&through=2026-09-05");
        await Page.Locator(".financial-report-filter button").First.ClickAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Financial Reports", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
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
        b.AddMarkupContent(0, "<!doctype html><html data-base-color='zinc' data-primary-color='blue' style='--radius:.5rem'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='portal-assets/css/app.css'><link rel='stylesheet' href='XFramework.Portal.styles.css'><style>body{padding:16px;margin:0;font-family:Arial}</style></head><body>");
        b.OpenComponent<FinancialReportFixtureSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class FinancialReportFixtureSurface : ComponentBase
{
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<XFramework.Portal.Features.Finance.Pages.Reports>(0);
        b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(4); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(5); b.CloseComponent();
    }
}
