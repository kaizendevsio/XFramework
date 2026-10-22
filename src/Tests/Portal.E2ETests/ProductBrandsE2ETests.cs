using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Text.Json;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Inventario.Integration.Drivers;
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
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

// Independent host and state: the shared setup wizard fixture is intentionally not used.
[TestFixture, NonParallelizable]
[Category("Kind:E2E"), Category("Module:Inventario"), Category("Area:Brands")]
public sealed class ProductBrandsE2ETests : PageTest
{
    private WebApplication _app = null!;
    private readonly BrandBrowserState _state = new();
    private readonly ConcurrentQueue<string> _errors = new();
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ReducedMotion = ReducedMotion.Reduce, ColorScheme = ColorScheme.Light, DeviceScaleFactor = 1
    };

    [OneTimeSetUp]
    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        var manifestPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [WebHostDefaults.StaticWebAssetsKey] = manifestPath });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var styles = manifest.RootElement.GetProperty("Root").GetProperty("Children").EnumerateObject()
            .Single(x => x.Value.TryGetProperty("Asset", out var asset) && asset.ValueKind == JsonValueKind.Object &&
                asset.GetProperty("SubPath").GetString() == "XFramework.Portal.styles.css").Name;
        builder.Services.AddSingleton(new BrandBrowserAssets(styles));
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
        {
            options.DetailedErrors = true;
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero;
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
        builder.Services.AddSingleton(_state);
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(x =>
            x.SelectedTenantId == _state.Tenant && x.SelectedTenantName == "Brand browser tenant"));
        var availability = new Mock<IPortalModuleAvailability>();
        availability.SetupGet(x => x.ActiveTenantId).Returns(_state.Tenant);
        availability.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        builder.Services.AddSingleton(availability.Object);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetInventarioSetupRequest request, CancellationToken _) =>
            {
                request.Metadata.RequestedTenantId.Should().Be(_state.Tenant);
                return BrandBrowserState.Ok(new InventarioSetupResponse { TenantId = _state.Tenant, CanManage = _state.CanManage });
            });
        wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchProductBrandsRequest request, CancellationToken _) => _state.Search(request));
        wrapper.Setup(x => x.PreviewProductBrandMerge(It.IsAny<PreviewProductBrandMergeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PreviewProductBrandMergeRequest request, CancellationToken _) => _state.Preview(request));
        wrapper.Setup(x => x.MergeProductBrands(It.IsAny<MergeProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MergeProductBrandsRequest request, CancellationToken _) => _state.Merge(request));
        builder.Services.AddSingleton(wrapper.Object);
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        data.Setup(x => x.Query<ProductCategory>()).Returns(() =>
        {
            IEnumerable<ProductCategory> rows = [
                new() { Id = _state.Tools, TenantId = _state.Tenant, Name = "Tools" },
                new() { Id = _state.Apparel, TenantId = _state.Tenant, Name = "Apparel" },
                new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Foreign category" },
                new() { Id = Guid.NewGuid(), TenantId = _state.Tenant, Name = "Deleted category", IsDeleted = true }
            ];
            var query = new Mock<IRemoteQuery<ProductCategory>>(MockBehavior.Strict);
            query.Setup(x => x.Where(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
                .Returns((Expression<Func<ProductCategory, bool>> predicate) => { rows = rows.Where(predicate.Compile()); return query.Object; });
            query.Setup(x => x.NoCache()).Returns(query.Object);
            query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<ProductCategory, string?>>>()))
                .Returns((Expression<Func<ProductCategory, string?>> order) => { rows = rows.OrderBy(order.Compile()); return query.Object; });
            query.Setup(x => x.Take(1000)).Returns(() => { rows = rows.Take(1000); return query.Object; });
            query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => rows.ToList());
            return query.Object;
        });
        builder.Services.AddSingleton(data.Object);
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root!.FullName, "src/Presentation/XFramework.Portal/wwwroot")),
            RequestPath = "/portal-assets"
        });
        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root.FullName, "src/Presentation/XFramework.Portal.Shared/wwwroot")),
            RequestPath = "/_content/XFramework.Portal.Shared"
        });
        _app.UseAntiforgery();
        _app.MapRazorComponents<BrandBrowserRoot>().AddInteractiveServerRenderMode();
        await _app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); }
    }

    [SetUp]
    public void Reset()
    {
        _state.Reset();
        _errors.Clear();
        Page.PageError += (_, error) => _errors.Enqueue(error);
        Page.Console += (_, message) => { if (message.Type == "error") _errors.Enqueue(message.Text); };
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Brands_CategoryChange_ListsAndSearchesOnlyScopedBrands(int width, int height)
    {
        await Open(width, height);
        await Expect(Combo("Brand")).ToHaveCountAsync(0);
        await SelectCategory("Tools");
        await Expect(BrandCell("ACME TOOLS")).ToBeVisibleAsync();
        await Expect(BrandCell("FABRIC HOUSE")).ToHaveCountAsync(0);
        await Page.GetByPlaceholder("Search all brands in category...").FillAsync("ACME TOOLS");
        await Page.GetByPlaceholder("Search all brands in category...").PressAsync("Tab");
        await Expect(BrandCell("ACME TOOLS")).ToBeVisibleAsync();
        await Expect(BrandCell("ACM TOOLS")).ToHaveCountAsync(0);
        await Screenshot("tools-grid", width, height);
        await SelectCategory("Apparel");
        await Expect(Page.GetByPlaceholder("Search all brands in category...")).ToBeEmptyAsync();
        await Expect(BrandCell("FABRIC HOUSE")).ToBeVisibleAsync();
        await Expect(BrandCell("ACME TOOLS")).ToHaveCountAsync(0);
        await Screenshot("apparel-grid", width, height);
        _state.Searches.Should().Contain(x => x.CategoryId == _state.Tools).And.Contain(x => x.CategoryId == _state.Apparel);
        _state.Searches.Should().Contain(x => x.CategoryId == _state.Tools && x.Search == "ACME TOOLS");
        _state.Searches.Should().OnlyContain(x => x.Metadata.RequestedTenantId == _state.Tenant && x.Limit == 50);
        _state.Merges.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Brands_Merge_RequiresConfirmationAndPreservesOtherCategory(int width, int height)
    {
        await Open(width, height);
        await SelectCategory("Tools");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Merge ACM TOOLS", Exact = true }).ClickAsync();
        await Expect(Combo("Merge brand")).ToContainTextAsync("ACM TOOLS");
        await ChooseBrand("Into brand", "ACME TOOLS");
        var review = Page.GetByRole(AriaRole.Button, new() { Name = "Review merge", Exact = true });
        await review.ClickAsync();
        var confirmation = Page.Locator("[role='dialog']:visible, [role='alertdialog']:visible")
            .Filter(new() { HasText = "Change 2 product(s)" }).Last;
        await Expect(confirmation).ToBeVisibleAsync();
        await Expect(confirmation).ToContainTextAsync("from \"ACM TOOLS\" to \"ACME TOOLS\"");
        await Expect(confirmation).ToContainTextAsync("Other categories, stock and prices will not change.");
        _state.Merges.Should().BeEmpty();
        await Screenshot("merge-confirmation", width, height);
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(review).ToBeEnabledAsync();
        _state.Merges.Should().BeEmpty();
        await Expect(Combo("Merge brand")).ToContainTextAsync("ACM TOOLS");
        await review.ClickAsync();
        await Expect(confirmation).ToBeVisibleAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Merge brands", Exact = true }).ClickAsync();
        await Expect(review).ToHaveCountAsync(0);
        await Expect(BrandCell("ACME TOOLS")).ToBeVisibleAsync();
        var request = _state.Merges.Should().ContainSingle().Subject;
        request.Confirmed.Should().BeTrue();
        request.ExpectedProductCount.Should().Be(2);
        request.Metadata.RequestedTenantId.Should().Be(_state.Tenant);
        request.CategoryId.Should().Be(_state.Tools);
        request.SourceBrand.Should().Be("ACM TOOLS");
        request.TargetBrand.Should().Be("ACME TOOLS");
        _state.Previews.Should().HaveCount(2).And.OnlyContain(x => x.Metadata.RequestedTenantId == _state.Tenant && x.CategoryId == _state.Tools);
        _state.Brands(_state.Apparel).Should().Contain("ACM TOOLS");
        await Expect(BrandCell("ACM TOOLS")).ToHaveCountAsync(0);
        await Screenshot("merged-grid", width, height);
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Brands_NonManagerPreferences_AllowSuggestionsButHideMerge(int width, int height)
    {
        _state.CanManage = false;
        await Open(width, height);
        await Expect(Page.GetByText("Tenant management permission is required to merge brands.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Merge brands", Exact = true })).ToHaveCountAsync(0);
        await SelectCategory("Tools");
        await Expect(BrandCell("ACME TOOLS")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Merge ACME TOOLS", Exact = true })).ToHaveCountAsync(0);
        await Screenshot("read-only", width, height);
        _state.Previews.Should().BeEmpty();
        _state.Merges.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Picker_NewBrand_NormalizesWhitespaceAndSupportsClear(int width, int height)
    {
        await Open(width, height, "/brand-picker-browser-fixture");
        await Combo("Brand").ClickAsync();
        await Page.GetByPlaceholder("Search brands", new() { Exact = true }).FillAsync("  new  brand ");
        await Expect(Option("Use \"NEW BRAND\" (new)")).ToBeVisibleAsync();
        await Screenshot("new-brand-option", width, height);
        await Option("Use \"NEW BRAND\" (new)").ClickAsync();
        await Expect(Page.Locator("output")).ToHaveTextAsync("NEW BRAND");
        _state.Searches.Should().Contain(x => x.Search == "NEW BRAND" && x.CategoryId == _state.Tools && x.Metadata.RequestedTenantId == _state.Tenant);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear brand", Exact = true }).ClickAsync();
        await Expect(Page.Locator("output")).ToBeEmptyAsync();
        await Expect(Combo("Brand")).Not.ToContainTextAsync("NEW BRAND");
        _state.Merges.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Brands_GridPagesAndFilters_AndSearchFindsBeyondBoundedResults(bool dark)
    {
        if (dark) await Page.AddInitScriptAsync("localStorage.setItem('bb-theme', JSON.stringify({baseColor:'slate',primaryColor:'blue',radius:0.5,isDarkMode:true}))");
        _state.SetBrands(_state.Tools, Enumerable.Range(1, 61).Select(x => $"BRAND {x:000}"));
        await Open(1920, 1080);
        (await Page.Locator("html").EvaluateAsync<bool>("element => element.classList.contains('dark')")).Should().Be(dark);
        await SelectCategory("Tools");
        await Expect(BrandCell("BRAND 001")).ToBeVisibleAsync();
        await Expect(BrandCell("BRAND 011")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Showing the first 50 matching brands. Refine the search to find other brands.")).ToBeVisibleAsync();
        await Screenshot(dark ? "brands-grid-page-1-dark" : "brands-grid-page-1", 1920, 1080);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true })
            .Or(Page.GetByRole(AriaRole.Link, new() { Name = "Next", Exact = true })).ClickAsync();
        await Expect(BrandCell("BRAND 011")).ToBeVisibleAsync();
        await Expect(BrandCell("BRAND 001")).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Columnheader).Filter(new() { HasText = "Brand" }).HoverAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Filter Brand", Exact = true }).ClickAsync();
        await Page.GetByPlaceholder("Enter value...").FillAsync("BRAND 023");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(BrandCell("BRAND 023")).ToBeVisibleAsync();
        await Expect(BrandCell("BRAND 011")).ToHaveCountAsync(0);
        await Screenshot(dark ? "brands-grid-column-filter-dark" : "brands-grid-column-filter", 1920, 1080);
        await Page.GetByPlaceholder("Search all brands in category...").FillAsync("BRAND 061");
        await Page.GetByPlaceholder("Search all brands in category...").PressAsync("Tab");
        await Expect(BrandCell("BRAND 061")).ToBeVisibleAsync();
        await Expect(BrandCell("BRAND 023")).ToHaveCountAsync(0);
        _state.Searches.Should().Contain(x => x.Search == "BRAND 061" && x.Limit == 50);
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Brands_ReadFailureDisplaysError_NotEmptyGrid_AndRetryRecovers()
    {
        _state.DenySearch = true;
        await Open(1920, 1080);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Brands could not be loaded.");
        await Expect(Page.GetByText("No matching brands", new() { Exact = true })).ToHaveCountAsync(0);
        await Screenshot("brands-load-error", 1920, 1080);
        _state.DenySearch = false;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Retry brands", Exact = true }).ClickAsync();
        await Expect(BrandCell("FABRIC HOUSE")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        _errors.Should().BeEmpty();
    }

    private async Task Open(int width, int height, string path = "/inventario/brands")
    {
        await Page.SetViewportSizeAsync(width, height);
        var response = await Page.GotoAsync(_app.Urls.Single() + path);
        response!.Status.Should().Be(200);
        if (path == "/inventario/brands")
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true })).ToBeVisibleAsync();
        else await Expect(Combo("Brand")).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-base-color", "slate");
        foreach (var href in await Page.Locator("link[rel='stylesheet']").EvaluateAllAsync<string[]>("links => links.map(x => x.href)"))
            (await Page.APIRequest.GetAsync(href)).Status.Should().Be(200, href);
    }

    private ILocator Combo(string label) => Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });
    private ILocator BrandCell(string name) => Page.GetByRole(AriaRole.Gridcell, new() { Name = name, Exact = true });
    private ILocator Option(string label) => Page.GetByRole(AriaRole.Option, new() { Name = label, Exact = true });
    private async Task SelectCategory(string category)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true }).ClickAsync();
        await Expect(Option("Foreign category")).ToHaveCountAsync(0);
        await Expect(Option("Deleted category")).ToHaveCountAsync(0);
        await Option(category).ClickAsync();
    }
    private async Task ChooseBrand(string label, string brand)
    {
        await Combo(label).ClickAsync();
        await Option(brand).ClickAsync();
    }

    private async Task Screenshot(string stage, int width, int height)
    {
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue("no horizontal overflow");
        (await Page.Locator(".brand-picker:visible").EvaluateAllAsync<bool>("""
            elements => elements.every(element => {
                const r = element.getBoundingClientRect();
                return r.width > 100 && r.left >= 0 && r.right <= innerWidth &&
                    [...element.querySelectorAll('button')].every(button => {
                        const b = button.getBoundingClientRect();
                        return b.left >= r.left - 1 && b.right <= r.right + 1;
                    });
            })
            """)).Should().BeTrue("picker controls fit their containers");
        (await Page.Locator(".brands-fields:visible, .brand-merge:visible").EvaluateAllAsync<bool>("""
            groups => groups.every(group => {
                const bounds = [...group.children].filter(x => x.getClientRects().length)
                    .map(x => x.getBoundingClientRect());
                return bounds.every((a, index) => bounds.slice(index + 1).every(b =>
                    a.right <= b.left + 1 || b.right <= a.left + 1 ||
                    a.bottom <= b.top + 1 || b.bottom <= a.top + 1));
            })
            """)).Should().BeTrue("category and brand controls must not overlap");
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "product-brands", $"{stage}-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var png = await Page.ScreenshotAsync(new() { Path = path, FullPage = true, Animations = ScreenshotAnimations.Disabled });
        TestContext.AddTestAttachment(path);
        png.Take(8).Should().Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)).Should().Be(width);
        BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)).Should().BeGreaterThanOrEqualTo(height);
        // Assert the captured pixels are nonblank, not merely that a screenshot file exists.
        var colors = await Page.EvaluateAsync<int>("""
            async encoded => {
                const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
                const bitmap = await createImageBitmap(new Blob([bytes], {type:'image/png'}));
                const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
                const context = canvas.getContext('2d');
                context.drawImage(bitmap, 0, 0);
                const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                const colors = new Set();
                for (let i = 0; i < pixels.length; i += 16)
                    colors.add((pixels[i] << 16) | (pixels[i+1] << 8) | pixels[i+2]);
                bitmap.close();
                return colors.size;
            }
            """, Convert.ToBase64String(png));
        colors.Should().BeGreaterThan(24, "captured UI must contain rendered controls and text");
    }
}

public sealed class BrandBrowserState
{
    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid Tools { get; } = Guid.NewGuid();
    public Guid Apparel { get; } = Guid.NewGuid();
    public bool CanManage { get; set; } = true;
    public bool DenySearch { get; set; }
    public ConcurrentQueue<SearchProductBrandsRequest> Searches { get; } = new();
    public ConcurrentQueue<PreviewProductBrandMergeRequest> Previews { get; } = new();
    public ConcurrentQueue<MergeProductBrandsRequest> Merges { get; } = new();
    private readonly Dictionary<Guid, List<string>> _brands = [];
    public void Reset()
    {
        lock (_brands)
        {
            _brands[Tools] = ["ACM TOOLS", "ACM TOOLS", "ACME TOOLS"];
            _brands[Apparel] = ["FABRIC HOUSE", "ACM TOOLS"];
        }
        CanManage = true;
        DenySearch = false;
        Searches.Clear(); Previews.Clear(); Merges.Clear();
    }
    public List<string> Brands(Guid category) { lock (_brands) return _brands[category].ToList(); }
    public void SetBrands(Guid category, IEnumerable<string> brands) { lock (_brands) _brands[category] = brands.ToList(); }
    public QueryResponse<List<string>> Search(SearchProductBrandsRequest request)
    {
        Searches.Enqueue(request);
        request.Metadata.RequestedTenantId.Should().Be(Tenant);
        if (DenySearch) return new() { HttpStatusCode = HttpStatusCode.Forbidden };
        return Ok(Brands(request.CategoryId).Distinct().Where(x => x.Contains(ProductBrand.Normalize(request.Search) ?? "", StringComparison.Ordinal))
            .OrderBy(x => x).Take(request.Limit).ToList());
    }
    public QueryResponse<ProductBrandMergePreview> Preview(PreviewProductBrandMergeRequest request)
    {
        Previews.Enqueue(request);
        request.Metadata.RequestedTenantId.Should().Be(Tenant);
        CanManage.Should().BeTrue();
        return Ok(new ProductBrandMergePreview(request.CategoryId, request.SourceBrand, request.TargetBrand,
            Brands(request.CategoryId).Count(x => x == request.SourceBrand && x != request.TargetBrand)));
    }
    public QueryResponse<ProductBrandMergePreview> Merge(MergeProductBrandsRequest request)
    {
        request.Metadata.RequestedTenantId.Should().Be(Tenant);
        CanManage.Should().BeTrue();
        request.Confirmed.Should().BeTrue();
        lock (_brands)
        {
            var brands = _brands[request.CategoryId];
            var count = brands.Count(x => x == request.SourceBrand && x != request.TargetBrand);
            count.Should().Be(request.ExpectedProductCount);
            _brands[request.CategoryId] = brands.Select(x => x == request.SourceBrand ? request.TargetBrand : x).ToList();
            Merges.Enqueue(request);
            return Ok(new ProductBrandMergePreview(request.CategoryId, request.SourceBrand, request.TargetBrand, count));
        }
    }
    public static QueryResponse<T> Ok<T>(T value) => new() { HttpStatusCode = HttpStatusCode.OK, Response = value };
}

public sealed record BrandBrowserAssets(string Styles);

[Route("/inventario/brands"), Route("/brand-picker-browser-fixture")]
public sealed class BrandBrowserRoot : ComponentBase
{
    [Inject] public BrandBrowserAssets Assets { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, $"<!doctype html><html lang='en'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='icon' href='data:,'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='portal-assets/css/app.css'><link rel='stylesheet' href='portal-assets/css/utilities.css'><link rel='stylesheet' href='{Assets.Styles}'><style>body{{margin:0;padding:16px}}</style></head><body class='font-sans antialiased'>");
        b.OpenComponent<BrandBrowserSurface>(1);
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false));
        b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class BrandBrowserSurface : ComponentBase
{
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public ThemeService Theme { get; set; } = null!;
    [Inject] public BrandBrowserState State { get; set; } = null!;
    private string? _brand;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Theme.InitializeAsync();
    }
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        if (new Uri(Navigation.Uri).AbsolutePath == "/brand-picker-browser-fixture")
        {
            b.OpenComponent<ProductBrandPicker>(0);
            b.AddAttribute(1, "CategoryId", State.Tools);
            b.AddAttribute(2, "Value", _brand);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string?>(this, value => _brand = value));
            b.CloseComponent();
            b.OpenElement(4, "output"); b.AddAttribute(5, "aria-label", "Selected brand"); b.AddContent(6, _brand); b.CloseElement();
        }
        else { b.OpenComponent<Brands>(7); b.CloseComponent(); }
        b.OpenComponent<XfContainerPortalHost>(8); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(9); b.CloseComponent();
        b.OpenComponent<BbDialogProvider>(10); b.CloseComponent();
        b.OpenComponent<BbToastProvider>(11); b.CloseComponent();
    }
}
