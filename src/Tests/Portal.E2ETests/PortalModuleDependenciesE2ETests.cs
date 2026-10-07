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
        var warning = Page.GetByTestId("module-dependency-warning");
        await Expect(warning).ToBeVisibleAsync();
        await warning.GetByRole(AriaRole.Button, new() { Name = "Show affected features", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("module-dependency-issues")).ToContainTextAsync("POS Registers");
        await Expect(Page.GetByTestId("module-dependency-issues")).ToContainTextAsync("Warehousing (disabled)");
        await registers.ClickAsync();
        await Expect(Page.GetByTestId("module-feature-dependencies")).ToContainTextAsync("Warehousing - Required - Disabled");

        await Page.GetByTestId("module-dependency-fix-all").ClickAsync();
        var confirmation = Page.GetByRole(AriaRole.Alertdialog, new() { Name = "Enable required features?", Exact = true });
        await Expect(confirmation).ToContainTextAsync("Warehousing");
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).Not.ToBeCheckedAsync();
        await Expect(warning).ToBeVisibleAsync();

        await Page.GetByTestId("module-dependency-fix-all").ClickAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Enable features", Exact = true }).ClickAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Expect(warning).ToBeHiddenAsync();
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync(new() { Timeout = 30_000 });
        var search = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search modules...", Exact = true });
        await search.FillAsync("Warehousing");
        await search.PressAsync("Tab");
        await Expect(registers).ToBeHiddenAsync();
        await Expect(warehouse).ToBeVisibleAsync();
        await ToggleFeature("Warehousing");
        await Expect(Page.GetByText("POS Registers requires Warehousing. Disable the dependent feature first.", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(warehouse.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await Expect(search).ToHaveValueAsync("Warehousing");
        await search.FillAsync("");
        await search.PressAsync("Tab");
        await Expect(registers).ToBeVisibleAsync();

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
        await VerifyTenantRoleCreationAsync();
    }

    private async Task VerifyTenantRoleCreationAsync()
    {
        var tenantUrl = Page.Url[..Page.Url.LastIndexOf("/modules", StringComparison.Ordinal)];
        await Page.GotoAsync(tenantUrl + "/role-types");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Role Type", Exact = true }).ClickAsync();
        var roleDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add Role Type", Exact = true });
        await roleDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("QA Cashier Role");
        await roleDialog.GetByRole(AriaRole.Button, new() { Name = "Add Role Group", Exact = true }).ClickAsync();
        var groupDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add Role Group", Exact = true });
        await groupDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("QA Cashiers");
        await groupDialog.GetByLabel("Description", new() { Exact = true }).FillAsync("Isolated UI regression; no user assignments.");
        await groupDialog.GetByRole(AriaRole.Button, new() { Name = "Create Role Group", Exact = true }).ClickAsync();
        await Expect(roleDialog).ToBeVisibleAsync();
        await Expect(roleDialog.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("QA Cashier Role");
        await Expect(roleDialog.GetByRole(AriaRole.Combobox)).ToContainTextAsync("QA Cashiers");
        await roleDialog.GetByRole(AriaRole.Button, new() { Name = "Create and Configure", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"^{Regex.Escape(tenantUrl)}/role-types/[a-f0-9-]+$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "QA Cashier Role", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tree)).ToBeVisibleAsync();
        await Page.GotoAsync(tenantUrl + "/role-types");
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { Has = Page.GetByRole(AriaRole.Gridcell, new() { Name = "QA Cashier Role", Exact = true }) });
        await Expect(row).ToContainTextAsync("QA Cashiers");
        await Page.GotoAsync(tenantUrl + "/reference-data");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reference Data", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Reference Data", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    private ILocator Feature(string name) => Page.GetByRole(AriaRole.Treeitem, new() { NameRegex = new Regex($"^{Regex.Escape(name)} ") });

    [TearDown]
    public async Task CaptureFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed) return;
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "module-dependencies");
        Directory.CreateDirectory(directory);
        var screenshot = Path.Combine(directory, "failure.png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        TestContext.AddTestAttachment(screenshot);
        if (await Page.GetByRole(AriaRole.Tree).CountAsync() > 0)
            TestContext.Out.WriteLine(await Page.GetByRole(AriaRole.Tree).First.InnerTextAsync());
    }

    // Blueprint's checkbox is presentation-only; its surrounding span handles pointer clicks.
    private Task ToggleFeature(string name) => Feature(name).Locator("[data-tree-checkbox]").First.ClickAsync();
}
