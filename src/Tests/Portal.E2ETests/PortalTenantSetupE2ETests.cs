using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:Portal")]
public sealed class PortalTenantSetupE2ETests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Environment.GetEnvironmentVariable("PORTAL_E2E_BASE_URL") ?? "http://127.0.0.1:5000",
        StorageStatePath = Environment.GetEnvironmentVariable("PORTAL_E2E_STORAGE_STATE"),
        ReducedMotion = ReducedMotion.Reduce
    };

    [TestCase(1920, 1080, false)]
    [TestCase(768, 1024, false)]
    [TestCase(390, 844, false)]
    [TestCase(1920, 1080, true)]
    [TestCase(768, 1024, true)]
    [TestCase(390, 844, true)]
    public async Task DefaultTenant_RegistersAndTransactionFilters_RenderWithoutErrors(int width, int height, bool dark)
    {
        var username = Environment.GetEnvironmentVariable("PORTAL_E2E_USERNAME");
        var password = Environment.GetEnvironmentVariable("PORTAL_E2E_PASSWORD");
        var tenantName = Environment.GetEnvironmentVariable("PORTAL_E2E_DEFAULT_TENANT_NAME");
        var usesSavedSession = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORTAL_E2E_STORAGE_STATE"));
        if (!usesSavedSession)
        {
            Assert.That(username, Is.Not.Null.And.Not.Empty, "Set PORTAL_E2E_USERNAME.");
            Assert.That(password, Is.Not.Null.And.Not.Empty, "Set PORTAL_E2E_PASSWORD.");
        }
        Assert.That(tenantName, Is.Not.Null.And.Not.Empty,
            "Set PORTAL_E2E_DEFAULT_TENANT_NAME for an isolated tenant with default feature toggles.");
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new() { ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light });
        if (usesSavedSession) await Page.GotoAsync("/");
        else
        {
            await Page.GotoAsync("/login");
            await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(username!);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password!);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        }
        var profile = Page.Locator("button.profile-trigger");
        await Expect(profile).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        try
        {
            await profile.ClickAsync();
            await Page.GetByRole(AriaRole.Combobox).First.ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = tenantName!, Exact = true }).ClickAsync();
            await Expect(profile).ToContainTextAsync(tenantName!);
            await Page.Keyboard.PressAsync("Escape");

            await Page.GotoAsync("/pos/registers");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Expect(Page.GetByTestId("pos-list-pager")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Registers could not load", Exact = true })).ToHaveCountAsync(0);
            await Expect(Page.GetByTestId("pos-register-setup-warning")).ToContainTextAsync("Inventario Warehousing");
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create Register", Exact = true })).ToBeDisabledAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
            await Expect(Page.GetByTestId("pos-list-pager")).ToBeVisibleAsync();

            await Page.GotoAsync("/inventario/transactions");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var surface = Page.GetByTestId("inventory-transactions-surface");
            var filters = Page.GetByTestId("inventory-transaction-filters");
            await Expect(filters).ToBeVisibleAsync(new() { Timeout = 30_000 });
            var surfaceBox = (await surface.BoundingBoxAsync())!;
            var boxes = new List<LocatorBoundingBoxResult>();
            foreach (var id in new[] { "transaction-product-filter", "transaction-from-filter", "transaction-through-filter" })
            {
                var wrapper = Page.GetByTestId(id);
                var box = (await wrapper.BoundingBoxAsync())!;
                box.X.Should().BeGreaterThanOrEqualTo(surfaceBox.X);
                (box.X + box.Width).Should().BeLessThanOrEqualTo(surfaceBox.X + surfaceBox.Width);
                box.Width.Should().BeGreaterThan(100);
                var control = (await wrapper.Locator("input, button").First.BoundingBoxAsync())!;
                control.X.Should().BeGreaterThanOrEqualTo(box.X);
                (control.X + control.Width).Should().BeLessThanOrEqualTo(box.X + box.Width + 1);
                boxes.Add(box);
            }
            for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i]; var b = boxes[j];
                (a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y)
                    .Should().BeFalse("filter fields must not overlap");
            }
            await Page.GetByLabel("Product search", new() { Exact = true }).FillAsync("no matching product");
            await Expect(surface.GetByRole(AriaRole.Heading, new() { Name = "No sales transactions found", Exact = true })).ToBeVisibleAsync();
            await Page.GetByTestId("transaction-from-filter").GetByRole(AriaRole.Button).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Grid)).ToBeVisibleAsync();
            await Page.Keyboard.PressAsync("Escape");
            await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "tenant-setup",
                $"transactions-{width}-{(dark ? "dark" : "light")}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
            TestContext.AddTestAttachment(path);
        }
        finally
        {
            await Page.GotoAsync("/");
            if (!usesSavedSession && await profile.IsVisibleAsync())
            {
                await profile.ClickAsync();
                await Page.GetByRole(AriaRole.Button, new() { Name = "Sign Out", Exact = true }).ClickAsync();
            }
        }
    }
}
