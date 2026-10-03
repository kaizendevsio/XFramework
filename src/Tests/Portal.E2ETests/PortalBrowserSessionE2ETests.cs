using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:Portal")]
public sealed class PortalBrowserSessionE2ETests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Environment.GetEnvironmentVariable("PORTAL_E2E_BASE_URL") ?? "http://127.0.0.1:5000",
        ReducedMotion = ReducedMotion.Reduce
    };

    [Test]
    public async Task Login_TabsShareSession_SeparateBrowserLogoutDoesNotSignOutOtherBrowser()
    {
        var username = Environment.GetEnvironmentVariable("PORTAL_E2E_USERNAME");
        var password = Environment.GetEnvironmentVariable("PORTAL_E2E_PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Assert.Ignore("Set PORTAL_E2E_USERNAME and PORTAL_E2E_PASSWORD to run authenticated browser checks.");
        }

        await using var separateBrowser = await Browser.NewContextAsync(ContextOptions());
        var separatePage = await separateBrowser.NewPageAsync();
        try
        {
            await SignInAsync(Page, username!, password!);
            var sameBrowserTab = await Context.NewPageAsync();
            await sameBrowserTab.GotoAsync("/");
            await Expect(sameBrowserTab.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }))
                .ToBeVisibleAsync();

            await separatePage.GotoAsync("/");
            await Expect(separatePage.GetByRole(AriaRole.Heading, new() { Name = "Sign in to Portal", Exact = true }))
                .ToBeVisibleAsync();
            await SignInAsync(separatePage, username!, password!);
            await SignOutAsync(separatePage);

            await Page.ReloadAsync();
            await sameBrowserTab.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }))
                .ToBeVisibleAsync();
            await Expect(sameBrowserTab.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }))
                .ToBeVisibleAsync();

            await SignOutAsync(Page);
            await sameBrowserTab.ReloadAsync();
            await Expect(sameBrowserTab.GetByRole(AriaRole.Heading, new() { Name = "Sign in to Portal", Exact = true }))
                .ToBeVisibleAsync();
        }
        finally
        {
            // End only the sessions created by this test, including after an assertion fails.
            await SignOutIfAuthenticatedAsync(Page);
            await SignOutIfAuthenticatedAsync(separatePage);
        }
    }

    private async Task SignInAsync(IPage page, string username, string password)
    {
        await page.GotoAsync("/login");
        await page.GetByLabel("Username", new() { Exact = true }).FillAsync(username);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    private async Task SignOutAsync(IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("button.profile-trigger").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign Out", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sign in to Portal", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    private async Task SignOutIfAuthenticatedAsync(IPage page)
    {
        await page.GotoAsync("/");
        if (await page.Locator("button.profile-trigger").IsVisibleAsync())
        {
            await SignOutAsync(page);
        }
    }
}
