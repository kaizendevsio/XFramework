using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:Portal")]
public sealed class PortalModuleDependenciesE2ETests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Environment.GetEnvironmentVariable("PORTAL_E2E_BASE_URL") ?? "http://127.0.0.1:5000",
        StorageStatePath = Environment.GetEnvironmentVariable("PORTAL_E2E_STORAGE_STATE"),
        ReducedMotion = ReducedMotion.Reduce
    };

    [Test]
    public async Task Modules_RequiredDependencies_ConfirmCancelPersistAndProtectPrerequisites()
    {
        Assert.That(Environment.GetEnvironmentVariable("PORTAL_E2E_ALLOW_TENANT_CREATION"), Is.EqualTo("1"),
            "This test creates an isolated QA tenant; set PORTAL_E2E_ALLOW_TENANT_CREATION=1 on a test deployment.");
        await Page.SetViewportSizeAsync(1920, 1080);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORTAL_E2E_STORAGE_STATE")))
        {
            var username = Environment.GetEnvironmentVariable("PORTAL_E2E_USERNAME");
            var password = Environment.GetEnvironmentVariable("PORTAL_E2E_PASSWORD");
            Assert.That(username, Is.Not.Null.And.Not.Empty);
            Assert.That(password, Is.Not.Null.And.Not.Empty);
            await Page.GotoAsync("/login");
            await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(username!);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password!);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
            await Expect(Page.Locator("button.profile-trigger")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        await Page.GotoAsync("/identity/tenants");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Tenant", Exact = true }).ClickAsync();
        var create = Page.GetByRole(AriaRole.Dialog);
        var name = $"Portal QA dependencies {DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        await create.GetByLabel("Name", new() { Exact = true }).FillAsync(name);
        await create.GetByLabel("Description", new() { Exact = true }).FillAsync("Isolated feature dependency regression; no business records or funds.");
        await create.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        var tenantRow = Page.GetByRole(AriaRole.Row).Filter(new() { Has = Page.GetByRole(AriaRole.Gridcell, new() { Name = name, Exact = true }) });
        await Expect(tenantRow).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await tenantRow.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        var modules = Page.GetByRole(AriaRole.Link, new() { Name = "Modules", Exact = true });
        if (!await modules.IsVisibleAsync())
            await Page.GetByRole(AriaRole.Button, new() { Name = "Tenant Detail", Exact = true }).ClickAsync();
        await modules.ClickAsync();
        var registers = Feature("POS Registers");
        var warehouse = Feature("Warehousing");
        await Expect(registers.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync(new() { Timeout = 30_000 });
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        await registers.ClickAsync();
        await Expect(Page.GetByTestId("module-feature-dependencies")).ToContainTextAsync("Warehousing - Required - Disabled");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable required features", Exact = true }).ClickAsync();
        var confirmation = Page.GetByRole(AriaRole.Alertdialog, new() { Name = "Enable required features?", Exact = true });
        await Expect(confirmation).ToContainTextAsync("Warehousing");
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable required features", Exact = true }).ClickAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Enable features", Exact = true }).ClickAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync(new() { Timeout = 30_000 });
        var search = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search modules...", Exact = true });
        await search.FillAsync("Warehousing");
        await ToggleFeature("Warehousing");
        await Expect(Page.GetByText("POS Registers requires Warehousing. Disable the dependent feature first.", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Expect(search).ToHaveValueAsync("Warehousing");
        await search.FillAsync("");

        // Disable dependents in order, then verify ordinary enabling also confirms prerequisites.
        foreach (var label in new[] { "POS Returns", "POS Sales", "POS Registers", "Warehousing" })
        {
            await ToggleFeature(label);
            await Expect(Feature(label).GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        }
        await ToggleFeature("POS Registers");
        await Expect(confirmation).ToContainTextAsync("Warehousing");
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(registers.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(registers.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        await ToggleFeature("POS Registers");
        await Expect(confirmation).ToContainTextAsync("Warehousing");
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Enable features", Exact = true }).ClickAsync();
        await Expect(registers.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "module-dependencies");
        Directory.CreateDirectory(directory);
        var screenshot = Path.Combine(directory, "required-features.png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        TestContext.AddTestAttachment(screenshot);
        TestContext.Out.WriteLine($"Isolated QA tenant retained for rechecks: {name}");
    }

    private ILocator Feature(string name) => Page.GetByRole(AriaRole.Treeitem, new() { NameRegex = new Regex($"^{Regex.Escape(name)} ") });

    // Blueprint's checkbox is presentation-only; its surrounding span handles pointer clicks.
    private Task ToggleFeature(string name) => Feature(name).Locator("[data-tree-checkbox]").First.ClickAsync();
}
