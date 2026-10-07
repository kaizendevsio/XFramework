using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using BlazorBlueprint.Components;
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
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed class InventarioSetupWizardE2ETests : PageTest
{
    private WebApplication _app = null!;
    private readonly SetupBrowserState _state = new();
    private readonly ConcurrentQueue<string> _errors = new();
    public override BrowserNewContextOptions ContextOptions() => new() { ReducedMotion = ReducedMotion.Reduce };

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
        builder.Services.AddSingleton(new SetupBrowserAssets(portalStyles));
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(_state);
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(() => _state.ActiveTenant);
        _state.Changed += () => tenant.Raise(x => x.OnChanged += null);
        builder.Services.AddSingleton(tenant.Object);
        var availability = new Mock<IPortalModuleAvailability>();
        availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        availability.SetupGet(x => x.ActiveTenantId).Returns(() => _state.ActiveTenant);
        availability.Setup(x => x.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(availability.Object);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetInventarioSetupRequest request, CancellationToken _) => new QueryResponse<InventarioSetupResponse>
            {
                HttpStatusCode = HttpStatusCode.OK, Response = new() { TenantId = request.Metadata.RequestedTenantId!.Value, CanManage = true, WarehousingEnabled = true,
                    DefaultCurrency = request.Metadata.RequestedTenantId == _state.FirstTenant ? "SGD" : "USD" }
            });
        wrapper.Setup(x => x.CompleteInventarioSetup(It.IsAny<CompleteInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .Returns((CompleteInventarioSetupRequest request, CancellationToken _) => _state.Complete(request));
        builder.Services.AddSingleton(wrapper.Object);
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseAntiforgery();
        _app.MapRazorComponents<SetupBrowserRoot>().AddInteractiveServerRenderMode();
        await _app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); } }

    [SetUp]
    public async Task Open()
    {
        _state.Reset(); _errors.Clear();
        Page.PageError += (_, error) => _errors.Enqueue(error);
        Page.Console += (_, message) => { if (message.Type == "error") TestContext.Progress.WriteLine(message.Text); };
        Page.Response += (_, response) => { if (response.Status >= 400) TestContext.Progress.WriteLine($"Resource HTTP {response.Status}: {response.Url}"); };
        Page.SetDefaultTimeout(8_000);
        await Page.SetViewportSizeAsync(1920, 1080);
        var response = await Page.GotoAsync(_app.Urls.Single() + "/inventario-setup-fixture");
        response!.Status.Should().Be(200);
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToBeVisibleAsync();
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
    [TestCase(390, 844)]
    public async Task Basic_ReviewConfirmAndReload_PreservesExplicitDefaultsAndCompletion(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Next();
        await Page.GetByLabel("Warehouse name", new() { Exact = true }).FillAsync("Confirmed QA warehouse");
        await Next();
        await Page.GetByLabel("Location name", new() { Exact = true }).FillAsync("Confirmed QA bin");
        await Next();
        await Page.GetByLabel("Default currency", new() { Exact = true }).FillAsync("SGD");
        await Next();
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("Confirmed QA warehouse");
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("Confirmed QA bin");
        await Expect(Page.GetByTestId("inventario-setup-review")).ToContainTextAsync("SGD");
        _state.Requests.Should().BeEmpty("all steps before confirmation are read-only");
        await Screenshot($"basic-review-{width}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm setup", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToBeVisibleAsync();
        var request = _state.Requests.Should().ContainSingle().Subject;
        request.Mode.Should().Be(InventarioSetupMode.Basic);
        request.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
        request.Warehouse!.Name.Should().Be("Confirmed QA warehouse");
        request.Location!.Name.Should().Be("Confirmed QA bin");
        request.DefaultCurrency.Should().Be("SGD");
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("setup-complete")).ToContainTextAsync("Basic");
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task Advanced_FullOptionsAndConfigurationLinks_AreAvailableWithoutFeatureWrites()
    {
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Setup mode", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Advanced", Exact = true }).ClickAsync();
        await Next();
        await Page.GetByLabel("Address", new() { Exact = true }).FillAsync("QA address");
        await Page.GetByLabel("Country code", new() { Exact = true }).FillAsync("SG");
        await Next();
        await Expect(Page.GetByLabel("Location type", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GetByLabel("Location description", new() { Exact = true }).FillAsync("QA inbound");
        await Next(); await Next();
        await Expect(Page.GetByRole(AriaRole.Navigation, new() { Name = "Inventario configuration" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Suppliers", Exact = true })).ToBeVisibleAsync();
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
        await Next();
        await Page.GetByLabel("Warehouse name", new() { Exact = true }).FillAsync("Discard me");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("setup-cancelled")).ToBeVisibleAsync();
        _state.Requests.Should().BeEmpty();
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("inventario-setup-wizard")).ToBeVisibleAsync();
        _state.Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Settings_TenantContextEvent_ReloadsAndRendersNewTenantDefaults()
    {
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Expect(Page.GetByLabel("Default currency", new() { Exact = true })).ToHaveValueAsync("SGD");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Switch QA tenant", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Default currency", new() { Exact = true })).ToHaveValueAsync("USD");
        _state.Requests.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task FailedConfirmation_PreservesDraftAndAllowsRetry()
    {
        await Next(); await Next(); await Next(); await Next();
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
        await Next(); await Next(); await Next(); await Next();
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

    private Task Next() => Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
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
public sealed class SetupBrowserRoot : ComponentBase
{
    [Inject] public SetupBrowserAssets Styles { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, $"<!doctype html><html data-base-color='zinc' data-primary-color='green'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='css/app.css'><link rel='stylesheet' href='{Styles.PortalStyles}'></head><body>");
        b.OpenComponent<SetupBrowserSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false)); b.CloseComponent();
        b.AddMarkupContent(2, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed record SetupBrowserAssets(string PortalStyles);

public sealed class SetupBrowserSurface : ComponentBase, IDisposable
{
    [Inject] public SetupBrowserState State { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    private bool _cancelled;
    protected override void OnInitialized() => State.Changed += Changed;
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<BbToastProvider>(0); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(1); b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(2); b.CloseComponent();
        b.OpenElement(3, "main"); b.AddAttribute(4, "style", "padding:24px;max-width:1000px;margin:auto");
        b.OpenElement(5, "h1"); b.AddContent(6, State.ActiveTenant == State.FirstTenant ? "QA Tenant A" : "QA Tenant B"); b.CloseElement();
        b.OpenComponent<BbButton>(7); b.AddAttribute(8, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, State.Switch)); b.AddAttribute(9, "ChildContent", (RenderFragment)(x => x.AddContent(0, "Switch QA tenant"))); b.CloseComponent();
        if (new Uri(Navigation.Uri).AbsolutePath == "/inventario-settings-fixture")
        { b.OpenComponent<XFramework.Portal.Features.Inventario.Pages.Settings>(10); b.CloseComponent(); }
        else if (_cancelled) { b.OpenElement(10, "p"); b.AddAttribute(11, "data-testid", "setup-cancelled"); b.AddContent(12, "Cancelled"); b.CloseElement(); }
        else if (State.Saved.TryGetValue(State.ActiveTenant, out var saved))
        { b.OpenElement(13, "p"); b.AddAttribute(14, "data-testid", "setup-complete"); b.AddContent(15, saved.Mode + " setup complete"); b.CloseElement(); }
        else
        {
            b.OpenComponent<InventarioSetupWizard>(16); b.SetKey(State.ActiveTenant);
            b.AddAttribute(17, "State", new InventarioSetupResponse { TenantId = State.ActiveTenant, CanManage = true, WarehousingEnabled = true });
            b.AddAttribute(18, "TenantName", State.ActiveTenant == State.FirstTenant ? "QA Tenant A" : "QA Tenant B");
            b.AddAttribute(19, "Completed", EventCallback.Factory.Create<InventarioSetupResponse>(this, _ => StateHasChanged()));
            b.AddAttribute(20, "Cancelled", EventCallback.Factory.Create(this, () => _cancelled = true)); b.CloseComponent();
        }
        b.CloseElement();
    }
    public void Dispose() => State.Changed -= Changed;
}

public sealed class SetupBrowserState
{
    public Guid FirstTenant { get; private set; }
    public Guid SecondTenant { get; private set; }
    public Guid ActiveTenant { get; private set; }
    public ConcurrentQueue<CompleteInventarioSetupRequest> Requests { get; } = new();
    public ConcurrentDictionary<Guid, InventarioSetupResponse> Saved { get; } = new();
    public TaskCompletionSource? Delay { get; set; }
    public bool FailNextConfirmation { get; set; }
    public event Action? Changed;
    public void Reset() { FirstTenant = Guid.NewGuid(); SecondTenant = Guid.NewGuid(); ActiveTenant = FirstTenant; Requests.Clear(); Saved.Clear(); Delay = null; FailNextConfirmation = false; }
    public void Switch() { ActiveTenant = SecondTenant; Changed?.Invoke(); }
    public async Task<QueryResponse<InventarioSetupResponse>> Complete(CompleteInventarioSetupRequest request)
    {
        Requests.Enqueue(request);
        if (Delay is not null) await Delay.Task;
        if (FailNextConfirmation) { FailNextConfirmation = false; return new() { HttpStatusCode = HttpStatusCode.ServiceUnavailable }; }
        var response = new InventarioSetupResponse { TenantId = request.Metadata.RequestedTenantId!.Value, Mode = request.Mode, CompletedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid(), LowStockThreshold = request.LowStockThreshold, DefaultCurrency = request.DefaultCurrency };
        Saved[response.TenantId] = response;
        return new() { HttpStatusCode = HttpStatusCode.OK, Response = response };
    }
}
