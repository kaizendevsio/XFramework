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

    [TestCase(1366, 768)]
    [TestCase(390, 844)]
    public async Task Login_FreshAnonymousSession_LoadsRuntimeAndAcceptsInput(int width, int height)
    {
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

        var wasDark = await Page.Locator("html").EvaluateAsync<bool>("e => e.classList.contains('dark')");
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = wasDark ? "Switch to light mode" : "Switch to dark mode", Exact = true
        }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new()
        {
            Name = wasDark ? "Switch to dark mode" : "Switch to light mode", Exact = true
        })).ToBeVisibleAsync();
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }
}
