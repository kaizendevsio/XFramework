using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
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
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Components.Layout;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Services;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Area:EntityPicker")]
public sealed class EntityPickerClickOutsideE2ETests : PageTest
{
    private WebApplication app = null!;
    private readonly ConcurrentQueue<string> errors = new();

    [OneTimeSetUp]
    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory,
                "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(options => options.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents(configureTheme: theme =>
        { theme.DefaultDarkMode = false; theme.DetectSystemPreference = false; });
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(s => s.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton<ClickOutsideCashierState>();
        builder.Services.AddScoped<TenantFilterService>();
        builder.Services.AddScoped<TenantModuleNavigationService>();
        builder.Services.AddScoped<NavigationHistoryService>();
        builder.Services.AddSingleton(Mock.Of<IDataContext>());
        builder.Services.AddSingleton(Mock.Of<IPortalModuleAvailability>());
        var fixtureActorId = Guid.NewGuid();
        builder.Services.AddSingleton(Mock.Of<IPortalActorContext>(a => a.CredentialId == fixtureActorId));
        builder.Services.AddScoped<IPortalTenantContext>(s => Mock.Of<IPortalTenantContext>(t =>
            t.SelectedTenantId == s.GetRequiredService<ClickOutsideCashierState>().TenantId));
        builder.Services.AddScoped(_ => new RequestMetadata());
        builder.Services.AddScoped<IPOSServiceWrapper>(s =>
        {
            var state = s.GetRequiredService<ClickOutsideCashierState>();
            var wrapper = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
            wrapper.Setup(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<List<PosCatalogItemResponse>>
                { HttpStatusCode = HttpStatusCode.OK, Response = [state.Item] });
            return wrapper.Object;
        });
        app = builder.Build();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapRazorComponents<ClickOutsideFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [TestCase(false, "select")]
    [TestCase(true, "select")]
    [TestCase(false, "escape")]
    [TestCase(true, "escape")]
    [TestCase(false, "advanced")]
    [TestCase(true, "advanced")]
    [TestCase(false, "outside")]
    [TestCase(true, "outside")]
    public async Task RegisterPicker_CloseWhileOutsideRegistrationCompletes_DoesNotLeakCallback(bool delayRegistration, string closeAction)
    {
        errors.Clear();
        Page.SetDefaultTimeout(5_000);
        Page.PageError += (_, error) => errors.Enqueue(error);
        await Page.SetViewportSizeAsync(1920, 1080);
        if (delayRegistration)
        {
            await Page.AddInitScriptAsync("window.releaseOutsideRegistration = false;");
            await Page.RouteAsync("**/primitives/click-outside.js", async route =>
            {
                var response = await route.FetchAsync();
                var script = await response.TextAsync();
                const string declaration = "export function onClickOutsideByIds(";
                script.Should().Contain(declaration);
                var start = script.IndexOf(declaration, StringComparison.Ordinal);
                var prefix = script[..start];
                var method = script[start..].Replace(declaration, "export async function onClickOutsideByIds(");
                const string marker = "cleanupRegistry.set(id, cleanupFunc);";
                method.Should().Contain(marker);
                method = method.Replace(marker, marker + "\nwindow.outsideRegistrationPending = true;\n" +
                    "while (!window.releaseOutsideRegistration) await new Promise(resolve => setTimeout(resolve, 10));");
                await route.FulfillAsync(new() { Response = response, Body = prefix + method });
            });
        }

        var response = await Page.GotoAsync(app.Urls.Single() + "/pos/cashier");
        response!.Status.Should().Be(200, await Page.ContentAsync());
        var picker = Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true });
        await picker.ClickAsync();
        var option = Page.GetByRole(AriaRole.Option, new() { Name = "Fixture register", Exact = true });
        await Expect(option).ToBeVisibleAsync();
        if (delayRegistration)
            await Page.WaitForFunctionAsync("window.outsideRegistrationPending === true");
        switch (closeAction)
        {
            case "select": await option.ClickAsync(); break;
            case "escape": await Page.Keyboard.PressAsync("Escape"); break;
            case "advanced": await Page.GetByRole(AriaRole.Option, new() { Name = "Advanced Search", Exact = true }).ClickAsync(); break;
            case "outside": await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync(); break;
        }
        await Expect(picker).ToHaveAttributeAsync("aria-expanded", "false");
        if (delayRegistration)
            await Page.EvaluateAsync("window.releaseOutsideRegistration = true");
        // The trigger closes before Blueprint's exit animation and portal teardown finish.
        await Expect(option).ToHaveCountAsync(0);
        if (closeAction == "advanced")
        {
            var finder = Page.GetByRole(AriaRole.Dialog, new() { Name = "Find register", Exact = true });
            await Expect(finder).ToBeVisibleAsync();
            await finder.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })
                .Filter(new() { HasNot = Page.Locator("svg") }).ClickAsync();
            await Expect(finder).ToBeHiddenAsync();
        }

        // Subsequent pointer clicks must not call a disposed .NET callback reference.
        for (var attempt = 0; attempt < 4; attempt++)
            await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await picker.ClickAsync();
        await option.ClickAsync();
        await Expect(picker).ToHaveAttributeAsync("aria-expanded", "false");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Fixture item", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Expect(Page.GetByTestId("pos-checkout")).ToBeEnabledAsync();
        for (var pass = 0; pass < 2; pass++)
        {
            await Page.GetByTestId("pos-checkout").ClickAsync();
            await Expect(Page.GetByTestId("pos-payment-stage")).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Back to cart", Exact = true }).ClickAsync();
            if (pass == 0)
                await Page.GetByRole(AriaRole.Button, new() { Name = "Enter cashier focus", Exact = true }).ClickAsync();
        }
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exit cashier focus", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Customize theme", Exact = true })).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(150);
        var artifacts = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "click-outside");
        Directory.CreateDirectory(artifacts);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"mainlayout-register-{closeAction}-delay-{delayRegistration}.png") });
        foreach (var error in errors) TestContext.Progress.WriteLine(error);
        errors.Should().BeEmpty("closing a register picker must remove its document listeners");
    }
}

[Route("/pos/cashier")]
public sealed class ClickOutsideFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html><head><base href='/'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='css/app.css'></head><body>");
        b.OpenComponent<ClickOutsideFixtureSurface>(1);
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false));
        b.CloseComponent();
        b.AddMarkupContent(3, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class ClickOutsideFixtureSurface : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<ClickOutsideFixtureLayout>(0);
        b.AddAttribute(1, "Body", (RenderFragment)(body =>
        { body.OpenComponent<ClickOutsideFixtureCashier>(0); body.CloseComponent(); }));
        b.CloseComponent();
    }
}

// Keep the production layout's renderer, theme controls and hosts; skip only remote bootstrap.
public sealed class ClickOutsideFixtureLayout : MainLayout
{
    protected override void OnInitialized() { }
    protected override Task OnInitializedAsync() => Task.CompletedTask;
    protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
}

public sealed class ClickOutsideCashierState
{
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid RegisterId { get; } = Guid.NewGuid();
    public Guid CurrencyId { get; } = Guid.NewGuid();
    public PosCatalogItemResponse Item { get; } = new()
    {
        ProductId = Guid.NewGuid(), SKU = "FIXTURE-1", DisplayName = "Fixture item",
        ProductName = "Fixture item", IsAvailable = true, Price = 10m
    };
}

// Render the real Cashier with an empty cart and no deployed services or credentials.
public sealed class ClickOutsideFixtureCashier : Cashier
{
    [Inject] public ClickOutsideCashierState State { get; set; } = null!;
    protected override void OnInitialized()
    {
        Set("_loading", false); Set("_hasTenant", true); Set("_moduleEnabled", true);
        Set("_registers", new List<PosRegisterResponse>
        { new() { Id = State.RegisterId, Name = "Fixture register", CurrencyId = State.CurrencyId } });
        Set("_selectedRegisterId", State.RegisterId.ToString());
        Set("_currencies", new List<CurrencyType>
        { new() { Id = State.CurrencyId, CurrencyIsoCode3 = "USD" } });
        Set("_catalogItems", new List<PosCatalogItemResponse> { State.Item });
    }
    protected override Task OnInitializedAsync() => Task.CompletedTask;
    private void Set(string name, object value) => typeof(Cashier)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
}
