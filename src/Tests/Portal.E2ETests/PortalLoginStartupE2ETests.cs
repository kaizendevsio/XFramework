using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:Portal")]
public sealed class PortalLoginStartupE2ETests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Environment.GetEnvironmentVariable("PORTAL_E2E_BASE_URL") ?? "http://127.0.0.1:5000",
        ReducedMotion = ReducedMotion.Reduce
    };

    [Test]
    public async Task Login_PhoneScannerReturnUrl_AuthenticatesAndOpensScanner()
    {
        var username = Environment.GetEnvironmentVariable("PORTAL_E2E_USERNAME");
        var password = Environment.GetEnvironmentVariable("PORTAL_E2E_PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            Assert.Ignore("Set PORTAL_E2E_USERNAME and PORTAL_E2E_PASSWORD for the authenticated scanner login test.");

        await Page.SetViewportSizeAsync(390, 844);
        try
        {
            await Page.GotoAsync("/pos/mobile-scanner", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login\\?ReturnUrl="));
            var usernameInput = Page.GetByLabel("Username", new() { Exact = true });
            await Expect(usernameInput).ToBeEnabledAsync(new() { Timeout = 30_000 });
            await usernameInput.FillAsync(username!);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password!);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Uri(new Uri(ContextOptions().BaseURL!), "/pos/mobile-scanner").ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Pair scanner", Exact = true }))
                .ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Start camera", Exact = true })).ToBeEnabledAsync();
            await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        }
        finally
        {
            // Revoke only the session created in this isolated browser context.
            await Page.GotoAsync("/auth/logout");
        }
    }

    [TestCase(1366, 768, ColorScheme.Light)]
    [TestCase(1366, 768, ColorScheme.Dark)]
    [TestCase(390, 844, ColorScheme.Light)]
    [TestCase(390, 844, ColorScheme.Dark)]
    [TestCase(320, 568, ColorScheme.Dark)]
    public async Task Login_FreshAnonymousSession_LoadsRuntimeAndAcceptsInput(int width, int height, ColorScheme colorScheme)
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = colorScheme });
        await Page.SetViewportSizeAsync(width, height);
        var runtime = await Page.APIRequest.GetAsync("/_framework/blazor.web.js");
        Assert.That(runtime.Status, Is.EqualTo(200), "The published Blazor runtime must be served, not just server health.");
        Assert.That(await runtime.TextAsync(), Does.Contain("Blazor"));

        await Page.GotoAsync("/login", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        var username = Page.GetByLabel("Username", new() { Exact = true });
        var password = Page.GetByLabel("Password", new() { Exact = true });
        await Expect(username).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Expect(password).ToBeEnabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true })).ToBeEnabledAsync();

        // Exercise typing without submitting credentials or creating a session.
        var input = Guid.NewGuid().ToString("N");
        await username.FillAsync("portal-startup-check");
        await password.FillAsync(input);
        await Expect(username).ToHaveValueAsync("portal-startup-check");
        await Expect(password).ToHaveValueAsync(input);

        await Expect(Page.Locator(".login-shell")).ToHaveTextAsync("XSystems Username Password Sign in");
        await Expect(Page.Locator(".login-form input:not([type=hidden])")).ToHaveCountAsync(2);
        Assert.That(await Page.EvaluateAsync<bool>(
            "document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        var card = await Page.Locator(".login-card").BoundingBoxAsync();
        Assert.That(card, Is.Not.Null);
        Assert.That(card!.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(card.X + card.Width, Is.LessThanOrEqualTo(width));
        Assert.That(card.Y + card.Height, Is.LessThanOrEqualTo(height));
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Login_BrandedHost_LoadsLogoOrFallsBackToWordmark(bool brokenLogo)
    {
        var url = Environment.GetEnvironmentVariable("PORTAL_E2E_BRANDED_URL");
        if (string.IsNullOrEmpty(url))
            Assert.Ignore("Set PORTAL_E2E_BRANDED_URL to a configured test host with a local logo.");
        if (brokenLogo)
            await Page.RouteAsync("**/favicon.svg", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GotoAsync(url!);
        await Expect(Page.GetByLabel("Username", new() { Exact = true })).ToBeEnabledAsync(new() { Timeout = 30_000 });
        if (brokenLogo)
        {
            await Expect(Page.Locator(".login-wordmark")).ToHaveTextAsync("Preview tenant");
            await Expect(Page.Locator(".login-logo")).ToHaveCountAsync(0);
        }
        else
        {
            await Expect(Page.Locator(".login-logo")).ToHaveAttributeAsync("alt", "Preview tenant");
            await Page.WaitForFunctionAsync("document.querySelector('.login-logo')?.naturalWidth > 0");
        }
        await Expect(Page.Locator("input[name=tenantId], input[name=roleId]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Login_RequiredFieldsAndErrorFeedbackRemainAvailable()
    {
        await Page.GotoAsync("/login?returnUrl=%2Fpos%2Fcashier&error=Sign-in%20failed");
        var username = Page.GetByLabel("Username", new() { Exact = true });
        await Expect(username).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Sign-in failed");
        await Expect(Page.Locator("input[name=returnUrl]")).ToHaveValueAsync("/pos/cashier");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        Assert.That(await username.EvaluateAsync<bool>("e => e.validity.valueMissing"), Is.True);
        await username.FillAsync("preview-user");
        Assert.That(await Page.GetByLabel("Password", new() { Exact = true })
            .EvaluateAsync<bool>("e => e.validity.valueMissing"), Is.True);
    }
}
