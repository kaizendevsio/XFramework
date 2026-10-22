using System.Collections.Concurrent;
using System.Net;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed partial class InventarioSetupWizardE2ETests : PageTest
{
    private WebApplication _app = null!;
    private Mock<IDataContext> _data = null!;
    private readonly SetupBrowserState _state = new();
    private readonly ConcurrentQueue<string> _errors = new();
    public override BrowserNewContextOptions ContextOptions() => new() { ReducedMotion = ReducedMotion.Reduce, ColorScheme = ColorScheme.Light };

    [OneTimeSetUp]
    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        using var manifest = JsonDocument.Parse(File.ReadAllText(builder.Configuration[WebHostDefaults.StaticWebAssetsKey]!));
        var portalStyles = manifest.RootElement.GetProperty("Root").GetProperty("Children").EnumerateObject()
            .Single(x => x.Value.TryGetProperty("Asset", out var asset) && asset.ValueKind == JsonValueKind.Object &&
                asset.GetProperty("SubPath").GetString() == "XFramework.Portal.styles.css").Name;
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var appSource = File.ReadAllText(Path.Combine(root!.FullName, "src", "Presentation", "XFramework.Portal", "Components", "App.razor"));
        var themeScript = Regex.Match(appSource, "<script>.*?</script>", RegexOptions.Singleline).Value;
        themeScript.Should().Contain("bb-theme", "the fixture must execute the Portal's actual theme initializer");
        builder.Services.AddSingleton(new SetupBrowserAssets(portalStyles, themeScript));
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
        {
            options.DefaultBaseColor = BaseColor.Slate;
            options.DefaultPrimaryColor = PrimaryColor.Blue;
        });
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(_state);
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(() => _state.ActiveTenant);
        tenant.SetupGet(x => x.SelectedTenantName).Returns(() => _state.ActiveTenant == _state.FirstTenant ? "QA Tenant A" : "QA Tenant B");
        _state.Changed += () => tenant.Raise(x => x.OnChanged += null);
        builder.Services.AddSingleton(tenant.Object);
        var availability = new Mock<IPortalModuleAvailability>();
        availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string _, string? feature) => _state.ProductWorkflow
                ? (feature is null or "catalog" or "warehousing" or "stock_balances" or "movements" or "variations")
                    && (feature is null || !_state.DisabledWorkflowFeatures.Contains(feature))
                : _state.VariationsOnly ? feature is null or "variations" : feature != "movements" || _state.MovementsEnabled);
        availability.SetupGet(x => x.ActiveTenantId).Returns(() => _state.ActiveTenant);
        availability.Setup(x => x.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(availability.Object);
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        _data = data;
        ReadRows<Product>(data, _state.BrowserProducts, applyFilters: true);
        ReadRows<ProductCategory>(data, () => _state.WorkflowCategories.ToList());
        ReadRows<Warehouse>(data, () => _state.Warehouses);
        ReadRows<InventoryLocation>(data, () => _state.Locations);
        ReadRows<ProductVariation>(data, () => _state.VariationsOnly || _state.PolishRuleScoped || _state.ProductWorkflowHasVariant
            ? [new ProductVariation { Id = _state.PolishVariantId, TenantId = _state.ActiveTenant, ProductId = _state.PolishProductId, Name = "Large", VariationType = "Size", Price = 20 }]
            : []);
        ReadRows<InventoryReorderRule>(data, () => [new InventoryReorderRule
        {
            Id = _state.PolishRuleId, TenantId = _state.ActiveTenant, ProductId = _state.PolishProductId,
            ProductVariationId = _state.PolishRuleScoped ? _state.PolishVariantId : null,
            WarehouseId = _state.PolishRuleScoped ? _state.Warehouses.Single().Id : null,
            LocationId = _state.PolishRuleScoped ? _state.Locations.Single().Id : null,
            MinimumQuantity = 2, ReorderPoint = 5, ReorderQuantity = 10, IsActive = true
        }]);
        ReadRows<InventoryLot>(data, () => []);
        ReadRows<StockBalance>(data, () => _state.WorkflowBalances.ToList());
        ReadRows<InventoryMovement>(data, () => []);
        ReadRows<Reservation>(data, () => []);
        ReadRows<Supplier>(data, () => [new Supplier { Id = _state.PolishSupplierId, TenantId = _state.ActiveTenant, Name = "Polish fixture supplier", IsActive = true }]);
        ReadRows<PurchaseOrder>(data, () => Enumerable.Range(1, 523).Select(i => new PurchaseOrder
            { Id = i == 1 ? _state.PolishOrderId : Guid.NewGuid(), TenantId = _state.ActiveTenant, SupplierId = _state.PolishSupplierId, OrderNumber = $"PO-{i:000}", OrderDate = DateTime.UtcNow.Date }).ToList());
        ReadRows<PurchaseOrderLine>(data, () => []);
        ReadRows<ReceivingDocument>(data, () => _state.DenyReceipts ? throw new UnauthorizedAccessException("Receiving access declined") : Enumerable.Range(1, 12).Select(i => new ReceivingDocument
            { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, SupplierId = _state.PolishSupplierId, ReceiptNumber = $"REC-{i:000}", ReceivedAt = DateTime.UtcNow.Date }).ToList());
        ReadRows<ReceivingLine>(data, () => [new ReceivingLine { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant,
            ProductId = _state.PolishProductId, InventoryMovementId = Guid.NewGuid(), Quantity = 12, UnitOfMeasure = "each",
            ReceivingDocument = new() { TenantId = _state.ActiveTenant, SupplierId = _state.PolishSupplierId } }]);
        builder.Services.AddSingleton(data.Object);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetProductVariationTypes(It.IsAny<GetProductVariationTypesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new QueryResponse<List<ProductVariationType>>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = [new ProductVariationType { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Name = "Size", Code = "SIZE" }]
            });
        wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetInventarioSetupRequest request, CancellationToken _) => new QueryResponse<InventarioSetupResponse>
            {
                HttpStatusCode = HttpStatusCode.OK, Response = _state.Get(request.Metadata.RequestedTenantId!.Value)
            });
        wrapper.Setup(x => x.CompleteInventarioSetup(It.IsAny<CompleteInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .Returns((CompleteInventarioSetupRequest request, CancellationToken _) => _state.Complete(request));
        wrapper.Setup(x => x.UpdateInventarioPreferences(It.IsAny<UpdateInventarioPreferencesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateInventarioPreferencesRequest request, CancellationToken _) => _state.Update(request));
        ConfigureProductWorkflow(wrapper, data);
        builder.Services.AddSingleton(wrapper.Object);
        builder.Services.AddSingleton(_stockFeatureIdentity.Object);
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseAntiforgery();
        _app.MapGet("/inventory-polish.css", () => Results.File(Path.Combine(root.FullName,
            "src/Presentation/XFramework.Portal.Features.Inventario/obj/Debug/net10.0/scopedcss/projectbundle/XFramework.Portal.Features.Inventario.bundle.scp.css"), "text/css"));
        _app.MapRazorComponents<SetupBrowserRoot>().AddInteractiveServerRenderMode();
        await _app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); } }

    [SetUp]
    public async Task Open()
    {
        _state.Reset(); _errors.Clear();
        _state.VariationsOnly = false;
        _state.PolishRuleScoped = false;
        Page.PageError += (_, error) => _errors.Enqueue(error);
        Page.Console += (_, message) => { if (message.Type == "error") TestContext.Progress.WriteLine(message.Text); };
        Page.Response += (_, response) => { if (response.Status >= 400) TestContext.Progress.WriteLine($"Resource HTTP {response.Status}: {response.Url}"); };
        Page.SetDefaultTimeout(8_000);
        await Page.SetViewportSizeAsync(1920, 1080);
        var response = await Page.GotoAsync(_app.Urls.Single() + "/inventario-setup-fixture");
        response!.Status.Should().Be(200);
        (await Page.APIRequest.GetAsync(_app.Urls.Single() + "/css/utilities.css")).Status.Should().Be(200);
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-primary-color", "blue");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-base-color", "slate");
    }

    [TearDown]
    public async Task CaptureFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed) return;
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventario-setup", "failure.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
        TestContext.Out.WriteLine(await Page.Locator("body").InnerTextAsync());
        foreach (var error in _errors) TestContext.Out.WriteLine(error);
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    [TestCase(390, 844)]
    public async Task Basic_ReviewConfirmAndReload_PreservesExplicitDefaultsAndCompletion(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GetByLabel("Stock area name (optional)", new() { Exact = true }).FillAsync("Home");
        await AssertWizardBounds();
        await Screenshot($"basic-organization-{width}");
        await Next();
        await Select("Default currency", "SGD");
        await DisableProductDefault();
        await Screenshot($"basic-defaults-{width}");
        await Next();
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("Home");
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("SGD");
        _state.Requests.Should().BeEmpty("all steps before confirmation are read-only");
        await Screenshot($"basic-review-{width}");
        await AssertWizardBounds();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToBeVisibleAsync();
        var request = _state.Requests.Should().ContainSingle().Subject;
        request.Mode.Should().Be(InventarioSetupMode.Basic);
        request.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
        request.UseSimpleStorage.Should().BeTrue();
        request.StockAreaName.Should().Be("Home");
        request.Warehouse.Should().BeNull(); request.Location.Should().BeNull();
        request.DefaultProductAvailable.Should().BeFalse();
        request.DefaultCurrency.Should().Be("SGD");
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToContainTextAsync("Basic");
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public async Task SetupPage_CenteredResponsiveSurface_ContainsHeaderAndInlineFooter(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-explicit-setup-fixture");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Inventario Setup", Exact = true })).ToBeVisibleAsync();
        await AssertWizardBounds();
        await Screenshot($"setup-page-{width}");
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public async Task Advanced_FullOptionsAndConfigurationLinks_AreAvailableWithoutFeatureWrites(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Select("Stock organization", "Multiple locations (Advanced)");
        await Next();
        await Page.GetByLabel("Address", new() { Exact = true }).FillAsync("QA address");
        await Page.GetByLabel("Country code", new() { Exact = true }).FillAsync("SG");
        await Screenshot($"advanced-warehouse-{width}");
        await Next();
        await Expect(Page.GetByLabel("Location type", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GetByLabel("Location description", new() { Exact = true }).FillAsync("QA inbound");
        await Next(); await Next();
        await Expect(Page.GetByRole(AriaRole.Navigation, new() { Name = "Inventario configuration" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Suppliers", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToContainTextAsync("Advanced");
        var request = _state.Requests.Should().ContainSingle().Subject;
        request.Warehouse!.AddressLine.Should().Be("QA address"); request.Warehouse.CountryCode.Should().Be("SG");
        request.Location!.Description.Should().Be("QA inbound");
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Cancel_DraftThenReload_DoesNotCallWrapperOrPersistMode()
    {
        await Page.GetByLabel("Stock area name (optional)", new() { Exact = true }).FillAsync("Discard me");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-cancelled")).ToBeVisibleAsync();
        _state.Requests.Should().BeEmpty();
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToBeVisibleAsync();
        _state.Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Products_CreateDialogAndWarnings_ConsumeSetupDefaultsWithoutLegacyReads()
    {
        _state.HasExistingConfiguration = true;
        _state.DefaultProductAvailable = false;
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-products-fixture");
        await Expect(Page.GetByText("SGD 17.00", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Low Stock", Exact = true }).Locator("..").Locator(".."))
            .ToContainTextAsync("1");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Product", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog).GetByLabel("Amount in Singapore Dollar", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Switch, new() { Name = "Available", Exact = true })).Not.ToBeCheckedAsync();
        await Screenshot("product-create-defaults");
        _state.Requests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Setup_ExplicitOrdinaryUserAccess_ShowsManagementRestriction()
    {
        _state.CanManage = false;
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-explicit-setup-fixture");
        await Expect(Page.GetByText("Setup restricted", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToHaveCountAsync(0);
        _state.Requests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Settings_TenantContextEvent_ReloadsAndRendersNewTenantDefaults()
    {
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true })).ToContainTextAsync("SGD");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Switch QA tenant", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true })).ToContainTextAsync("USD");
        _state.Requests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task FailedConfirmation_PreservesDraftAndAllowsRetry()
    {
        await Next(); await Next();
        _state.FailNextConfirmation = true;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Setup could not be saved");
        _state.Saved.Should().BeEmpty();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToBeVisibleAsync();
        _state.Requests.Should().HaveCount(2);
        _state.Requests.Select(x => x.CompletionRequestId).Distinct().Should().ContainSingle();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task TenantChangedDuringSave_CapturedTenantCannotCompleteNewTenant()
    {
        await Next(); await Next();
        _state.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Saving setup...", new() { Exact = true })).ToBeVisibleAsync();
        var request = _state.Requests.Should().ContainSingle().Subject;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Switch QA tenant", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "QA Tenant B", Exact = true })).ToBeVisibleAsync();
        _state.Delay.SetResult();
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToHaveCountAsync(0);
        request.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
        _state.Saved.ContainsKey(_state.SecondTenant).Should().BeFalse();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task ExistingConfiguration_SwitchPathsRepeatedly_KeepsBasicSelectableAndStepsOrdered()
    {
        SeedStorage();
        await Page.ReloadAsync();
        await Select("Stock organization", "Multiple locations (Advanced)");
        await Next();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Warehouse", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Back", Exact = true }).ClickAsync();
        await Select("Stock organization", "One stock area (Basic)");
        await Select("Stock organization", "Multiple locations (Advanced)");
        await Select("Stock organization", "One stock area (Basic)");
        await Next();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true })).ToBeVisibleAsync();
        await Next();
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("Basic");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToBeVisibleAsync();
        var request = _state.Requests.Should().ContainSingle().Subject;
        request.UseSimpleStorage.Should().BeTrue(); request.StockAreaName.Should().Be("My stock");
        request.Warehouse.Should().BeNull(); request.Location.Should().BeNull();
        _state.Warehouses.Should().HaveCount(2); _state.Locations.Should().HaveCount(2);
        _errors.Should().BeEmpty();
    }

    [TestCase("One stock area (Basic)")]
    [TestCase("Multiple locations (Advanced)")]
    public async Task WarehousingDisabled_CatalogOnly_HasNoStorageStepsOrWrites(string organization)
    {
        _state.WarehousingEnabled = false;
        await Page.ReloadAsync();
        await Select("Stock organization", organization);
        await Expect(Page.GetByText("Catalog only", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Stock area name (optional)", new() { Exact = true })).ToHaveCountAsync(0);
        await Next(); await Next();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToBeVisibleAsync();
        var request = _state.Requests.Should().ContainSingle().Subject;
        request.UseSimpleStorage.Should().BeFalse();
        request.Warehouse.Should().BeNull(); request.Location.Should().BeNull();
        request.ExistingWarehouseId.Should().BeNull(); request.ExistingLocationId.Should().BeNull();
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public async Task Settings_CompletedTenant_SavesModeAndDefaultsWithoutRepeatingSetup(int width, int height)
    {
        SeedStorage();
        var completed = DateTime.UtcNow.AddDays(-1);
        _state.Saved[_state.ActiveTenant] = _state.Get(_state.ActiveTenant) with { Mode = InventarioSetupMode.Advanced, CompletedAt = completed };
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Select("Setup mode", "Basic");
        await Select("Default currency", "USD");
        await DisableProductDefault();
        await Select("Stock area", "STORE - Store");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        await Select("Stock location", "SHELF - Store shelf");
        await AssertStockPickerWidths();
        await Screenshot($"settings-{width}");
        var bounds = await Page.GetByTestId("inventario-settings").BoundingBoxAsync();
        bounds!.Width.Should().BeLessThanOrEqualTo(960);
        Math.Abs(bounds.X + bounds.Width / 2 - width / 2f).Should().BeLessThan(2);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        var request = _state.PreferenceRequests.Should().ContainSingle().Subject;
        request.Mode.Should().Be(InventarioSetupMode.Basic); request.DefaultProductAvailable.Should().BeFalse();
        request.WarehouseId.Should().Be(_state.Warehouses[1].Id); request.LocationId.Should().Be(_state.Locations[1].Id);
        _state.Saved[_state.ActiveTenant].CompletedAt.Should().Be(completed);
        _state.Requests.Should().BeEmpty();
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Setup mode", Exact = true })).ToContainTextAsync("Basic");
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Stock area", Exact = true })).ToContainTextAsync("Store");
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-explicit-setup-fixture");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Change mode and settings", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToHaveCountAsync(0);
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task StockMovement_NewDialog_UsesSavedStockArea()
    {
        PrepareProductWorkflow();
        _state.Warehouses.Clear();
        _state.Locations.Clear();
        SeedStorage();
        _state.Saved[_state.ActiveTenant] = _state.Get(_state.ActiveTenant) with { WarehouseId = _state.Warehouses[1].Id, LocationId = _state.Locations[1].Id };
        await OpenWorkflowProduct();
        await WorkflowAvailableStock.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post Stock Movement", Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Post Stock Movement", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Warehouse", Exact = true })).ToContainTextAsync("Store");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Location", Exact = true })).ToContainTextAsync("Store shelf");
        _state.Requests.Should().BeEmpty(); _state.PreferenceRequests.Should().BeEmpty();
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CurrencyUnavailable_SettingsRequiresExplicitActiveCurrency(bool noCurrencies)
    {
        _state.Saved[_state.ActiveTenant] = _state.Get(_state.ActiveTenant) with { SupportedCurrencies = noCurrencies ? [] : ["USD"] };
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Select("Setup mode", "Advanced");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        if (noCurrencies)
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Finance settings", Exact = true })).ToHaveAttributeAsync("href", "/finance/settings");
        else
        {
            await Expect(Page.GetByText("Select an active currency", new() { Exact = true })).ToBeVisibleAsync();
            await Select("Default currency", "USD");
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeEnabledAsync();
        }
        _state.PreferenceRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task NoFinanceCurrencies_WizardCannotAdvanceToConfirmation()
    {
        _state.SupportedCurrencies.Clear();
        await Page.ReloadAsync();
        await Next();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Finance settings", Exact = true })).ToBeVisibleAsync();
        await Next();
        await Expect(Page.GetByTestId("inventario-setup-review")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true })).ToHaveCountAsync(0);
        _state.Requests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(1024, 768)]
    public async Task DarkTheme_SetupAndSettings_UsePortalThemeAndReadableOverlays(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-explicit-setup-fixture");
        await Expect(Page.Locator("html")).ToHaveClassAsync("dark");
        await Next();
        await AssertRenderedDarkTheme();
        await Screenshot($"setup-defaults-dark-{width}");
        await AssertWizardBounds();
        SeedStorage();
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Select("Stock area", "STORE - Store");
        await Select("Stock location", "SHELF - Store shelf");
        await AssertStockPickerWidths();
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Option, new() { Name = "USD", Exact = true })).ToBeVisibleAsync();
        await AssertRenderedDarkTheme();
        await Screenshot($"settings-dark-{width}");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.GetByRole(AriaRole.Option, new() { Name = "USD", Exact = true })).ToHaveCountAsync(0);
        await AssertRenderedDarkTheme();
        _state.Requests.Should().BeEmpty(); _state.PreferenceRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Settings_OnlyPickableLocations_SaveWaitsForCompleteSelection()
    {
        SeedStorage();
        _state.Locations.Add(new() { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = _state.Warehouses[1].Id, Code = "HOLD", Name = "Not pickable", IsEnabled = true, IsPickable = false });
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Select("Stock area", "STORE - Store");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Stock location", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Option, new() { Name = "HOLD - Not pickable", Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Option, new() { Name = "SHELF - Store shelf", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeEnabledAsync();
        _state.PreferenceRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task FinanceCurrencies_EmptyThenConfigured_ReloadsWithoutChangingSavedCurrency()
    {
        _state.SupportedCurrencies.Clear();
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Expect(Page.GetByText("No active currencies", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        _state.SupportedCurrencies.Add("USD");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reload", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Select an active currency", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true })).ToContainTextAsync("SGD");
        await Select("Default currency", "USD");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeEnabledAsync();
        _state.PreferenceRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Settings_DiscardChanges_RestoresDefaultsWithoutWrites()
    {
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Select("Setup mode", "Advanced");
        await Select("Default currency", "USD");
        await DisableProductDefault();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Setup mode", Exact = true })).ToContainTextAsync("Basic");
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true })).ToContainTextAsync("SGD");
        await Expect(Page.GetByRole(AriaRole.Switch, new() { Name = "Enable new products in catalog", Exact = true })).ToBeCheckedAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true })).ToBeDisabledAsync();
        _state.PreferenceRequests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    private void SeedStorage()
    {
        _state.HasExistingConfiguration = true;
        _state.Warehouses.AddRange([
            new() { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Code = "HOME", Name = "Home", IsEnabled = true },
            new() { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Code = "STORE", Name = "Store", IsEnabled = true }
        ]);
        _state.Locations.AddRange([
            new() { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = _state.Warehouses[0].Id, Code = "HOME", Name = "Home shelf", IsEnabled = true },
            new() { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = _state.Warehouses[1].Id, Code = "SHELF", Name = "Store shelf", IsEnabled = true }
        ]);
    }

    private Task Next() => Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
    private async Task DisableProductDefault()
    {
        var toggle = Page.GetByRole(AriaRole.Switch, new() { Name = "Enable new products in catalog", Exact = true });
        await toggle.ClickAsync();
        await Expect(toggle).Not.ToBeCheckedAsync();
    }
    private async Task Select(string label, string option)
    {
        await Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = option, Exact = true }).ClickAsync();
    }
    private async Task AssertWizardBounds()
    {
        var wizard = await Page.GetByTestId("inventario-setup-wizard").BoundingBoxAsync();
        wizard.Should().NotBeNull();
        wizard!.Width.Should().BeLessThanOrEqualTo(960);
        var viewport = Page.ViewportSize!;
        Math.Abs(wizard.X + wizard.Width / 2 - viewport.Width / 2).Should().BeLessThan(2);
        var footer = Page.GetByTestId("inventario-setup-footer");
        var footerBounds = await footer.BoundingBoxAsync();
        Math.Abs(footerBounds!.Width - wizard.Width).Should().BeLessThan(2, "footer must span the wizard width");
        var cancel = await footer.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).BoundingBoxAsync();
        var back = await footer.GetByRole(AriaRole.Button, new() { Name = "Back", Exact = true }).BoundingBoxAsync();
        Math.Abs(cancel!.Y - back!.Y).Should().BeLessThan(2, "Cancel and navigation share a single footer row");
        if (viewport.Width >= 768) (cancel.Y + cancel.Height).Should().BeLessThanOrEqualTo(viewport.Height);
    }
    private async Task AssertRenderedDarkTheme()
    {
        var root = Page.Locator("html");
        await Expect(root).ToHaveClassAsync("dark");
        await Expect(root).ToHaveAttributeAsync("data-base-color", "slate");
        await Expect(root).ToHaveAttributeAsync("data-primary-color", "blue");
        await Expect(root).ToHaveCSSAsync("--radius", "0.5rem");
        await Expect(Page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(30, 30, 30)");
        await Expect(Page.Locator("body")).ToHaveCSSAsync("color", "rgb(244, 244, 245)");
    }

    private async Task AssertStockPickerWidths()
    {
        var currency = await Page.GetByRole(AriaRole.Combobox, new() { Name = "Default currency", Exact = true }).BoundingBoxAsync();
        foreach (var label in new[] { "Stock area", "Stock location" })
        {
            var picker = await Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }).BoundingBoxAsync();
            Math.Abs(picker!.Width - currency!.Width).Should().BeLessThan(2, "stock pickers must fill their field columns");
        }
    }
    private static void ReadRows<T>(Mock<IDataContext> data, Func<List<T>> rows, bool applyFilters = false) where T : class
    {
        data.Setup(x => x.Query<T>()).Returns(() =>
        {
        var skip = 0;
        var take = int.MaxValue;
        var filters = new List<Func<T, bool>>();
        IEnumerable<T> FilteredRows() => rows().Where(row => filters.All(filter => filter(row)));
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>()))
            .Callback<Expression<Func<T, bool>>>(filter => { if (applyFilters) filters.Add(filter.Compile()); }).Returns(query.Object);
        query.Setup(x => x.Skip(It.IsAny<int>())).Callback<int>(value => skip = value).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Callback<int>(value => take = value).Returns(query.Object);
        query.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FilteredRows().Count());
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, object>>>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, object>>>())).Returns(query.Object);
        query.Setup(x => x.ThenBy(It.IsAny<Expression<Func<T, object>>>())).Returns(query.Object);
        query.Setup(x => x.ThenByDescending(It.IsAny<Expression<Func<T, object>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.ThenBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, Guid>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.ThenBy(It.IsAny<Expression<Func<T, Guid>>>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FilteredRows().Skip(skip).Take(take).ToList());
        query.Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FilteredRows().FirstOrDefault());
        return query.Object;
        });
    }
    private async Task Screenshot(string name)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventario-setup", name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth")).Should().BeTrue("wizard must fit the viewport");
    }
}

[Route("/inventario-setup-fixture")]
[Route("/inventario-settings-fixture")]
[Route("/inventario-products-fixture")]
[Route("/inventario-explicit-setup-fixture")]
[Route("/inventario-stock-fixture")]
[Route("/inventario-supplier-polish-fixture")]
[Route("/inventario-order-polish-fixture")]
[Route("/inventario-location-polish-fixture")]
[Route("/inventario-variations-polish-fixture")]
[Route("/inventario-rule-polish-fixture")]
[Route("/inventario/products/{ProductId:guid}")]
[Route("/inventario/products/{ProductId:guid}/{Section}")]
public sealed class SetupBrowserRoot : ComponentBase
{
    [Parameter] public Guid ProductId { get; set; }
    [Parameter] public string? Section { get; set; }
    [Inject] public SetupBrowserAssets Styles { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, $"<!doctype html><html lang='en'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='css/app.css'><link rel='stylesheet' href='css/utilities.css'><link rel='stylesheet' href='{Styles.PortalStyles}'>{Styles.ThemeScript}</head><body class='font-sans antialiased'>");
        b.AddMarkupContent(3, "<link rel='stylesheet' href='/inventory-polish.css'>");
        b.OpenComponent<SetupBrowserSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script>window.inventarioFocus={capture:()=>{},restore:()=>{}}</script><script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed record SetupBrowserAssets(string PortalStyles, string ThemeScript);

public sealed class SetupBrowserSurface : ComponentBase, IDisposable
{
    [Inject] public SetupBrowserState State { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public ThemeService Theme { get; set; } = null!;
    protected override async Task OnAfterRenderAsync(bool firstRender) { if (firstRender) await Theme.InitializeAsync(); }
    private bool _cancelled;
    protected override void OnInitialized()
    {
        State.Changed += Changed;
        Navigation.LocationChanged += LocationChanged;
    }
    private void LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs args) => Changed();
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<BbToastProvider>(0); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(1); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(2); b.CloseComponent();
        b.OpenComponent<BbDialogProvider>(3); b.CloseComponent();
        b.OpenElement(3, "main"); b.AddAttribute(4, "style", "padding:24px;margin:auto");
        b.OpenElement(5, "h1"); b.AddContent(6, State.ActiveTenant == State.FirstTenant ? "QA Tenant A" : "QA Tenant B"); b.CloseElement();
        b.OpenComponent<BbButton>(7); b.AddAttribute(8, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, State.Switch)); b.AddAttribute(9, "ChildContent", (RenderFragment)(x => x.AddContent(0, "Switch QA tenant"))); b.CloseComponent();
        var path = new Uri(Navigation.Uri).AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 3 or 4 && segments[0] == "inventario" && segments[1] == "products" && Guid.TryParse(segments[2], out var productId))
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.ProductDetail>(10); b.SetKey(productId); b.AddAttribute(11, "Id", productId); b.AddAttribute(12, "Section", segments.Length == 4 ? segments[3] : null); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-settings-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Settings>(10); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-products-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Products>(10); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-explicit-setup-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Setup>(10); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-stock-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Stock>(10); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-supplier-polish-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.SupplierDetail>(10); b.AddAttribute(11, "Id", State.PolishSupplierId); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-order-polish-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.PurchaseOrderDetail>(10); b.AddAttribute(11, "Id", State.PolishOrderId); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-location-polish-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.LocationDetail>(10); b.AddAttribute(11, "Id", State.Locations.First().Id); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-variations-polish-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.ProductDetail>(10); b.AddAttribute(11, "Id", State.PolishProductId); b.AddAttribute(12, "Section", "variations"); b.CloseComponent(); }
        else if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-rule-polish-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.ReorderRuleDetail>(10); b.AddAttribute(11, "Id", State.PolishRuleId); b.CloseComponent(); }
        else if (_cancelled) { b.OpenElement(10, "p"); b.AddAttribute(11, "data-testid", "setup-cancelled"); b.AddContent(12, "Cancelled"); b.CloseElement(); }
        else if (State.Saved.TryGetValue(State.ActiveTenant, out var saved))
        { b.OpenElement(13, "p"); b.AddAttribute(14, "data-testid", "setup-complete"); b.AddContent(15, saved.Mode + " setup complete"); b.CloseElement(); }
        else
        {
            b.OpenComponent<InventarioSetupWizard>(16); b.SetKey(State.ActiveTenant);
            b.AddAttribute(17, "State", State.Get(State.ActiveTenant));
            b.AddAttribute(18, "TenantName", State.ActiveTenant == State.FirstTenant ? "QA Tenant A" : "QA Tenant B");
            b.AddAttribute(19, "Completed", EventCallback.Factory.Create<InventarioSetupResponse>(this, _ => StateHasChanged()));
            b.AddAttribute(20, "Cancelled", EventCallback.Factory.Create(this, () => _cancelled = true)); b.CloseComponent();
        }
        b.CloseElement();
    }
    public void Dispose()
    {
        State.Changed -= Changed;
        Navigation.LocationChanged -= LocationChanged;
    }
}

public sealed partial class SetupBrowserState
{
    public bool VariationsOnly { get; set; }
    public bool PolishRuleScoped { get; set; }
    public Guid PolishRuleId { get; } = Guid.NewGuid();
    public Guid PolishVariantId { get; } = Guid.NewGuid();
    public bool DenyReceipts { get; set; }
    public bool MovementsEnabled { get; set; } = true;
    public Guid PolishSupplierId { get; } = Guid.NewGuid();
    public Guid PolishProductId { get; } = Guid.NewGuid();
    public Guid PolishOrderId { get; } = Guid.NewGuid();
    public Guid FirstTenant { get; private set; }
    public Guid SecondTenant { get; private set; }
    public Guid ActiveTenant { get; private set; }
    public ConcurrentQueue<CompleteInventarioSetupRequest> Requests { get; } = new();
    public ConcurrentQueue<UpdateInventarioPreferencesRequest> PreferenceRequests { get; } = new();
    public ConcurrentDictionary<Guid, InventarioSetupResponse> Saved { get; } = new();
    public TaskCompletionSource? Delay { get; set; }
    public bool FailNextConfirmation { get; set; }
    public bool CanManage { get; set; } = true;
    public bool HasExistingConfiguration { get; set; }
    public bool WarehousingEnabled { get; set; } = true;
    public bool DefaultProductAvailable { get; set; } = true;
    public List<Warehouse> Warehouses { get; } = [];
    public List<InventoryLocation> Locations { get; } = [];
    public List<string> SupportedCurrencies { get; } = ["PHP", "SGD", "USD"];
    public InventarioSetupResponse Get(Guid tenantId) => Saved.TryGetValue(tenantId, out var saved) ? saved : new()
    {
        TenantId = tenantId, CanManage = CanManage, HasExistingConfiguration = HasExistingConfiguration,
        WarehousingEnabled = WarehousingEnabled, LowStockThreshold = 19, SupportedCurrencies = SupportedCurrencies,
        DefaultCurrency = tenantId == FirstTenant ? "SGD" : "USD", DefaultProductAvailable = DefaultProductAvailable,
        Warehouses = Warehouses, Locations = Locations
    };
    public QueryResponse<InventarioSetupResponse> Update(UpdateInventarioPreferencesRequest request)
    {
        PreferenceRequests.Enqueue(request);
        var state = Get(request.Metadata.RequestedTenantId!.Value);
        var response = state with { Mode = request.Mode ?? state.Mode, WarehouseId = request.WarehouseId ?? state.WarehouseId,
            LocationId = request.LocationId ?? state.LocationId, DefaultCurrency = request.DefaultCurrency,
            LowStockThreshold = request.LowStockThreshold, DefaultProductAvailable = request.DefaultProductAvailable, ConcurrencyStamp = Guid.NewGuid() };
        Saved[state.TenantId] = response;
        return new() { HttpStatusCode = HttpStatusCode.OK, Response = response };
    }
    public event Action? Changed;
    public void Reset() { ResetProductWorkflow(); DenyReceipts = false; MovementsEnabled = true; FirstTenant = Guid.NewGuid(); SecondTenant = Guid.NewGuid(); ActiveTenant = FirstTenant; Requests.Clear(); PreferenceRequests.Clear(); Saved.Clear(); Warehouses.Clear(); Locations.Clear(); SupportedCurrencies.Clear(); SupportedCurrencies.AddRange(["PHP", "SGD", "USD"]); Delay = null; FailNextConfirmation = false; CanManage = true; HasExistingConfiguration = false; WarehousingEnabled = true; DefaultProductAvailable = true; }
    public void Switch() { ActiveTenant = SecondTenant; Changed?.Invoke(); }
    public async Task<QueryResponse<InventarioSetupResponse>> Complete(CompleteInventarioSetupRequest request)
    {
        Requests.Enqueue(request);
        if (Delay is not null) await Delay.Task;
        if (FailNextConfirmation) { FailNextConfirmation = false; return new() { HttpStatusCode = HttpStatusCode.ServiceUnavailable }; }
        var response = Get(request.Metadata.RequestedTenantId!.Value) with { Mode = request.Mode, CompletedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid(), LowStockThreshold = request.LowStockThreshold, DefaultCurrency = request.DefaultCurrency, DefaultProductAvailable = request.DefaultProductAvailable };
        Saved[response.TenantId] = response;
        return new() { HttpStatusCode = HttpStatusCode.OK, Response = response };
    }
}
