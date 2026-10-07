using System.Collections.Concurrent;
using System.Net;
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
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Services;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Area:CashierFocus")]
public sealed class PosCashierBrowserFullscreenE2ETests : PageTest
{
    private WebApplication app = null!;
    private readonly ConcurrentQueue<string> errors = new();
    private ILocator Cashier => Page.GetByTestId("pos-cashier");
    private ILocator Toggle => Page.Locator("[data-pos-focus-toggle]");
    private ILocator Sidebar => Page.GetByRole(AriaRole.Complementary, new() { Name = "Portal navigation" });

    [OneTimeSetUp]
    public async Task StartFixture()
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
        var actorId = Guid.NewGuid();
        builder.Services.AddSingleton(Mock.Of<IPortalActorContext>(actor => actor.CredentialId == actorId));
        builder.Services.AddScoped<IPortalTenantContext>(s => Mock.Of<IPortalTenantContext>(tenant =>
            tenant.SelectedTenantId == s.GetRequiredService<ClickOutsideCashierState>().TenantId));
        builder.Services.AddScoped(_ => new RequestMetadata());
        builder.Services.AddScoped<IPOSServiceWrapper>(s =>
        {
            var state = s.GetRequiredService<ClickOutsideCashierState>();
            // No sale, held-cart, inventory or payment mutation can reach a deployed service.
            var wrapper = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
            wrapper.Setup(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<List<PosCatalogItemResponse>>
                { HttpStatusCode = HttpStatusCode.OK, Response = [state.Item] });
            return wrapper.Object;
        });
        app = builder.Build();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapStaticAssets(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "XFramework.Portal.staticwebassets.endpoints.json"));
        app.MapRazorComponents<CashierFocusFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopFixture()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    [SetUp]
    public void CaptureErrors()
    {
        errors.Clear();
        Page.SetDefaultTimeout(10_000);
        Page.PageError += (_, error) => errors.Enqueue(error);
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400)
                errors.Enqueue($"HTTP {response.Status}: {response.Url}");
        };
        Page.Console += (_, message) =>
        {
            if (message.Type == "error")
                errors.Enqueue(message.Text);
        };
    }

    [TearDown]
    public async Task CheckErrors()
    {
        if (!Page.IsClosed)
            await SaveScreenshot("finished");
        errors.Should().BeEmpty("fullscreen rejection must not break the cashier or leak callbacks");
    }

    [Test]
    public async Task Focus_TrustedClick_RequestsRealFullscreenAndSurvivesServerRender()
    {
        await Page.AddInitScriptAsync("""
            const nativeRequest = Element.prototype.requestFullscreen;
            document.addEventListener('click', event => window.focusClickTrusted = event.isTrusted, true);
            Element.prototype.requestFullscreen = function(...args) {
                window.focusActivation = { active: navigator.userActivation.isActive,
                    trusted: window.focusClickTrusted, root: this === document.documentElement };
                return nativeRequest.apply(this, args);
            };
            """);
        await OpenFixture();
        await Toggle.ClickAsync();
        await AssertFocused(fullscreen: true);
        (await Page.EvaluateAsync<bool>("window.focusActivation.active && window.focusActivation.trusted && window.focusActivation.root"))
            .Should().BeTrue("the native API must run directly in the trusted browser click, not after server interop");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#pos-catalog-search")).ToBeFocusedAsync();
        await AssertFocused(fullscreen: true);
        await Toggle.ClickAsync();
        await AssertNormal();
    }

    [TestCase("browser")]
    [TestCase("escape")]
    public async Task Focus_BrowserExitOrEscape_RestoresFocusUi(string exit)
    {
        await OpenFixture();
        await Toggle.ClickAsync();
        await AssertFocused(fullscreen: true);
        if (exit == "browser")
            await Page.EvaluateAsync("document.exitFullscreen()");
        else
            await Page.Keyboard.PressAsync("Escape");
        await AssertNormal();
    }

    [TestCase("unsupported")]
    [TestCase("policy")]
    [TestCase("denied")]
    [TestCase("throws")]
    public async Task Focus_FullscreenUnavailable_FallsBackAndCanExit(string failure)
    {
        await Page.AddInitScriptAsync(failure switch
        {
            "unsupported" => "Element.prototype.requestFullscreen = undefined;",
            "policy" => "Object.defineProperty(document, 'fullscreenEnabled', { get: () => false });",
            "denied" => "Element.prototype.requestFullscreen = () => Promise.reject(new DOMException('Fixture denied', 'NotAllowedError'));",
            _ => "Element.prototype.requestFullscreen = () => { throw new DOMException('Fixture denied', 'NotAllowedError'); };"
        });
        await OpenFixture();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Toggle.ClickAsync();
            await AssertFocused(fullscreen: false);
            await Expect(Toggle).ToHaveAttributeAsync("title", "Exit cashier focus (fullscreen unavailable)");
            if (attempt == 0)
                await Toggle.ClickAsync();
            else
                await Page.Keyboard.PressAsync("Escape");
            await AssertNormal();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Focus_PendingRequestThenExitOrDispose_DoesNotReenter(bool dispose)
    {
        await Page.AddInitScriptAsync("""
            const nativeRequest = Element.prototype.requestFullscreen;
            Element.prototype.requestFullscreen = function(...args) {
                return nativeRequest.apply(this, args).then(() => new Promise(resolve => {
                    window.releaseFocusRequest = () => { resolve(); window.focusRequestReleased = true; };
                }));
            };
            const add = document.addEventListener.bind(document);
            const remove = document.removeEventListener.bind(document);
            window.focusListeners = 0;
            document.addEventListener = function(type, listener, options) {
                if (['onEscape', 'onFullscreenChange'].includes(listener.name)) window.focusListeners++;
                return add(type, listener, options);
            };
            document.removeEventListener = function(type, listener, options) {
                if (['onEscape', 'onFullscreenChange'].includes(listener.name)) window.focusListeners--;
                return remove(type, listener, options);
            };
            """);
        await OpenFixture();
        await Toggle.ClickAsync();
        await AssertFocused(fullscreen: true);
        await Page.WaitForFunctionAsync("typeof window.releaseFocusRequest === 'function'");
        if (dispose)
        {
            await Page.Locator("#fixture-leave").EvaluateAsync("button => button.click()");
            await Expect(Cashier).ToHaveCountAsync(0);
            await Page.WaitForFunctionAsync("window.focusListeners === 0 && document.fullscreenElement === null");
        }
        else
        {
            await Toggle.ClickAsync();
            await AssertNormal();
        }
        await Page.EvaluateAsync("window.releaseFocusRequest()");
        await Page.EvaluateAsync("new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        (await Page.EvaluateAsync<bool>("document.fullscreenElement === null")).Should().BeTrue();
        if (!dispose)
            await AssertNormal();
    }

    [Test]
    public async Task Focus_ExitRejected_StillRestoresLayoutAndObservesBrowserExit()
    {
        await OpenFixture();
        await Toggle.ClickAsync();
        await AssertFocused(fullscreen: true);
        await Page.EvaluateAsync("""
            window.nativeExitFullscreen = document.exitFullscreen.bind(document);
            document.exitFullscreen = () => Promise.reject(new DOMException('Fixture exit denied', 'NotAllowedError'));
            void 0;
            """);
        await Toggle.ClickAsync();
        await Expect(Cashier).ToHaveAttributeAsync("data-focus", "false");
        await Expect(Sidebar).ToBeVisibleAsync();
        await Page.EvaluateAsync("window.nativeExitFullscreen()");
        await AssertNormal();
    }

    [TestCase(1920, 1080, false)]
    [TestCase(1920, 1080, true)]
    [TestCase(1366, 768, false)]
    [TestCase(1366, 768, true)]
    [TestCase(768, 1024, false)]
    [TestCase(768, 1024, true)]
    [TestCase(390, 844, false)]
    [TestCase(390, 844, true)]
    public async Task Focus_ViewportAndTheme_PreservesCartAndControls(int width, int height, bool dark)
    {
        await Page.SetViewportSizeAsync(width, height);
        await OpenFixture();
        if (dark)
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Switch to dark mode", Exact = true }).ClickAsync();
            await Expect(Page.Locator("html")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bdark\b"));
        }
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Fixture item", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Toggle.ClickAsync();
            await AssertFocused(fullscreen: true);
            await Expect(Page.Locator("#pos-catalog-search")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
            await Expect(Page.Locator(".pos-cart-line")).ToHaveCountAsync(1);
            var pay = Page.GetByTestId("pos-checkout");
            await Expect(pay).ToBeEnabledAsync();
            await pay.ScrollIntoViewIfNeededAsync();
            await Expect(pay).ToBeInViewportAsync(new() { Ratio = .99f });
            (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth + 2"))
                .Should().BeTrue("fullscreen must not introduce horizontal overflow");
            await SaveScreenshot($"focused-{width}-{height}-{dark}-{attempt}");
            await Toggle.ClickAsync();
            await AssertNormal();
        }
    }

    private async Task OpenFixture()
    {
        var response = await Page.GotoAsync(app.Urls.Single() + "/focus-fixture");
        response!.Status.Should().Be(200);
        await Expect(Toggle).ToBeEnabledAsync();
        await Expect(Cashier).ToBeVisibleAsync();
        await Expect(Sidebar).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("0.00");
    }

    private async Task AssertFocused(bool fullscreen)
    {
        await Expect(Cashier).ToHaveAttributeAsync("data-focus", "true");
        await Expect(Toggle).ToHaveAccessibleNameAsync("Exit cashier focus");
        await Expect(Toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Sidebar).ToBeHiddenAsync();
        await Page.WaitForFunctionAsync(fullscreen
            ? "document.fullscreenElement === document.documentElement"
            : "document.fullscreenElement === null");
        await Expect(Toggle.Locator("[data-pos-focus-exit-icon]")).ToBeVisibleAsync();
    }

    private async Task AssertNormal()
    {
        await Page.WaitForFunctionAsync("document.fullscreenElement === null");
        await Expect(Cashier).ToHaveAttributeAsync("data-focus", "false");
        await Expect(Toggle).ToHaveAccessibleNameAsync("Enter cashier focus");
        await Expect(Toggle).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(Cashier).ToHaveAttributeAsync("data-fullscreen", "false");
        await Expect(Sidebar).ToBeVisibleAsync();
        await Expect(Toggle.Locator("[data-pos-focus-enter-icon]")).ToBeVisibleAsync();
    }

    private async Task SaveScreenshot(string suffix)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "cashier-focus");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{TestContext.CurrentContext.Test.ID}-{suffix}.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
    }
}

[Route("/focus-fixture")]
public sealed class CashierFocusFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!doctype html><html><head><base href='/'><link rel='icon' href='favicon.svg'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='css/app.css'></head><body>");
        b.OpenComponent<CashierFocusFixtureSurface>(1);
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender: false));
        b.CloseComponent();
        b.AddMarkupContent(3, "<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class CashierFocusFixtureSurface : ComponentBase
{
    private bool showCashier = true;

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<ClickOutsideFixtureLayout>(0);
        b.AddAttribute(1, "Body", (RenderFragment)(body =>
        {
            body.OpenElement(0, "button");
            body.AddAttribute(1, "id", "fixture-leave");
            body.AddAttribute(2, "hidden", true);
            body.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => showCashier = false));
            body.CloseElement();
            if (showCashier)
            {
                body.OpenComponent<ClickOutsideFixtureCashier>(4);
                body.CloseComponent();
            }
        }));
        b.CloseComponent();
    }
}
