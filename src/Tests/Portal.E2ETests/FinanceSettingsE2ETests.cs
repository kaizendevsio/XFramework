using System.Net;
using System.Linq.Expressions;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
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
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using Wallets.Domain.Shared.Contracts;
using Wallets.Integration.Drivers;
using XFramework.Portal.Features.Finance.Services;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
public sealed class FinanceSettingsE2ETests : PageTest
{
    private WebApplication app = null!;
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid currency = Guid.NewGuid();
    private bool canManage = true;
    private bool wrongTenant;
    private bool needsRepair;
    private UpdateTenantFinanceSettingsRequest? saved;

    [OneTimeSetUp]
    public async Task Start()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent!;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json") });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(x => x.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
        {
            options.DefaultBaseColor = BaseColor.Slate;
            options.DefaultPrimaryColor = PrimaryColor.Blue;
            options.DefaultDarkMode = false;
            options.DefaultRadius = 0.5;
            options.PersistToLocalStorage = true;
        });
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(x => x.SelectedTenantId == tenant && x.SelectedTenantName == "Finance fixture tenant"));
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response());
        identity.Setup(x => x.UpdateTenantFinanceSettings(It.IsAny<UpdateTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateTenantFinanceSettingsRequest r, CancellationToken _) => { saved = r; return Response(r.Settings); });
        builder.Services.AddSingleton(identity.Object);
        builder.Services.AddScoped<NavigationHistoryService>();
        builder.Services.AddScoped<WalletsAdminBackendContractService>();
        builder.Services.AddScoped(_ => new RequestMetadata { RequestedTenantId = tenant });
        builder.Services.AddSingleton(Mock.Of<IWalletsServiceWrapper>());
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var wallets = new Mock<IRemoteQuery<Wallet>>(MockBehavior.Strict);
        wallets.Setup(x => x.IgnoreQueryFilters()).Returns(wallets.Object);
        wallets.Setup(x => x.NoCache()).Returns(wallets.Object);
        wallets.Setup(x => x.Include(It.IsAny<Expression<Func<Wallet, WalletType>>>())).Returns(wallets.Object);
        wallets.Setup(x => x.Where(It.IsAny<Expression<Func<Wallet, bool>>>())).Returns(wallets.Object);
        wallets.Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Wallet
        {
            Id = WalletSummaryFixtureSurface.WalletId, TenantId = tenant, AccountNumber = "QA-WALLET",
            Balance = 100, DebitOnHoldBalance = 20, CreditOnHoldBalance = 5, TransferableBalance = 70,
            WalletType = new WalletType { Name = "QA wallet" }, CreatedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        data.Setup(x => x.Query<Wallet>()).Returns(wallets.Object);
        builder.Services.AddSingleton(data.Object);
        app = builder.Build();
        app.UseStaticFiles();
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal/wwwroot")), RequestPath = "/portal-assets" });
        app.UseAntiforgery();
        app.MapRazorComponents<FinanceSettingsFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();
    }

    private QueryResponse<TenantFinanceSettingsResponse> Response(TenantFinanceSettings? settings = null) => new()
    {
        HttpStatusCode = HttpStatusCode.OK,
        Response = new()
        {
            TenantId = wrongTenant ? Guid.NewGuid() : tenant, CanManage = canManage, ConcurrencyStamp = Guid.NewGuid(),
            IsUsable = !needsRepair, ConfigurationError = needsRepair ? "Select active currencies and save to repair." : null,
            Settings = settings ?? new() { DefaultCurrencyId = currency, SupportedCurrencyIds = [currency], TaxEnabled = true, TaxRatePercent = 9 },
            AvailableCurrencies = [new() { Id = currency, Code = "SGD", Name = "Singapore dollar" }],
            SupportedCurrencies = [new() { Id = currency, Code = "SGD", Name = "Singapore dollar" }]
        }
    };

    [SetUp]
    public void Reset() { canManage = true; wrongTenant = false; needsRepair = false; saved = null; }

    [OneTimeTearDown]
    public async Task Stop() { await app.StopAsync(); await app.DisposeAsync(); }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Settings_BoundedForm_UpdatesThroughWrapper_WithTenantScope(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.AddInitScriptAsync("localStorage.setItem('bb-theme', JSON.stringify({ baseColor: 'slate', primaryColor: 'blue', radius: 0.5, isDarkMode: false }));");
        await Page.GotoAsync(app.Urls.Single() + "/finance-settings-fixture");
        await Expect(Page.GetByText("Finance fixture tenant", new() { Exact = true })).ToBeVisibleAsync();
        var input = Page.GetByLabel("Tax rate (%)", new() { Exact = true });
        await Expect(input).ToBeEnabledAsync();
        await input.FillAsync("12");
        await input.BlurAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Finance settings saved.", new() { Exact = true })).ToBeVisibleAsync();
        saved.Should().NotBeNull();
        saved!.Metadata.RequestedTenantId.Should().Be(tenant);
        saved.Settings.TaxRatePercent.Should().Be(12);
        saved.Settings.SupportedCurrencyIds.Should().Equal(currency);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-base-color", "slate");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-primary-color", "blue");
        await Expect(Page.Locator("html")).ToHaveCSSAsync("--radius", "0.5rem");
        var bounds = await Page.GetByTestId("finance-settings").BoundingBoxAsync();
        bounds!.Width.Should().BeLessThanOrEqualTo(961);
        Math.Abs(bounds.X - (width - bounds.Width) / 2).Should().BeLessThan(2);
        var saveBounds = await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).BoundingBoxAsync();
        var inputBounds = await input.BoundingBoxAsync();
        saveBounds!.Y.Should().BeGreaterThan(inputBounds!.Y + inputBounds.Height);
        (await input.BoundingBoxAsync())!.Width.Should().BeLessThanOrEqualTo(161);
        (await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })
            .EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor")).Should().NotBe("rgba(0, 0, 0, 0)");
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth")).Should().BeTrue();
        (await Page.APIRequest.GetAsync(app.Urls.Single() + "/portal-assets/css/utilities.css")).Status.Should().Be(200);
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", $"finance-settings-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task WalletSummary_LoadsWithoutUnrelatedSectionQueries_AndExplainsHolds(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/wallet-summary-fixture");
        await Expect(Page.GetByText("Posted balance", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Section unavailable", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "100.00", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "80.00", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "85.00", Exact = true })).ToBeVisibleAsync();
        await Page.GetByText("Total including holds", new() { Exact = true }).HoverAsync();
        await Expect(Page.GetByText("Posted balance plus credit holds minus debit holds. Matches posted balance when there are no holds.", new() { Exact = true })).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth")).Should().BeTrue();
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", $"wallet-summary-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
    }

    [Test]
    public async Task Settings_AdminRepairDraft_RemainsEditableWithWarning()
    {
        needsRepair = true;
        await Page.GotoAsync(app.Urls.Single() + "/finance-settings-fixture");
        await Expect(Page.GetByText("Select active currencies and save to repair.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Tax rate (%)", new() { Exact = true })).ToBeEnabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeEnabledAsync();
        await Expect(Page.GetByText("Added to the discounted sale amount.", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Settings_ReadOnlyUser_CannotSave()
    {
        canManage = false;
        await Page.GotoAsync(app.Urls.Single() + "/finance-settings-fixture");
        await Expect(Page.GetByLabel("Tax rate (%)", new() { Exact = true })).ToBeDisabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
        saved.Should().BeNull();
    }

    [Test]
    public async Task Settings_WrongTenantResponse_IsNotDisplayedOrEditable()
    {
        wrongTenant = true;
        await Page.GotoAsync(app.Urls.Single() + "/finance-settings-fixture");
        await Expect(Page.GetByText("Finance settings returned for a different tenant. Refresh to retry.")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToHaveCountAsync(0);
    }
}

[Route("/finance-settings-fixture")]
[Route("/wallet-summary-fixture")]
public sealed class FinanceSettingsFixtureRoot : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html data-base-color='slate' data-primary-color='blue' style='--radius:0.5rem'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='portal-assets/css/app.css'><link rel='stylesheet' href='portal-assets/css/utilities.css'><link rel='stylesheet' href='XFramework.Portal.styles.css'><style>body{padding:16px;margin:0;font-family:Arial}</style></head><body>");
        b.OpenComponent(1, Navigation.Uri.Contains("wallet-summary-fixture") ? typeof(WalletSummaryFixtureSurface) : typeof(FinanceSettingsFixtureSurface)); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class WalletSummaryFixtureSurface : ComponentBase
{
    public static readonly Guid WalletId = Guid.Parse("2c524ddb-23a0-41e6-af49-0180837cf040");
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<XFramework.Portal.Features.Finance.Pages.WalletDetail>(0);
        b.AddAttribute(1, "Id", WalletId); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(2); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(3); b.CloseComponent();
        b.OpenComponent<BbToastProvider>(4); b.CloseComponent();
    }
}

public sealed class FinanceSettingsFixtureSurface : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<XFramework.Portal.Features.Finance.Pages.Settings>(0); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(1); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(2); b.CloseComponent();
        b.OpenComponent<BbToastProvider>(3); b.CloseComponent();
    }
}
