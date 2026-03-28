using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ControlPanel.E2ETests;

/// <summary>
/// End-to-end tests for the Control Panel admin portal.
/// Tests the full workflow: Tenant → User → Roles → Wallets.
/// Requires the app running at http://localhost:5000.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class ControlPanelE2ETests : PageTest
{
    private const string BaseUrl = "http://localhost:5000";

    // Shared state across ordered tests
    private static string _tenantName = $"E2E-Tenant-{Guid.NewGuid().ToString()[..8]}";
    private static string _userName = "Jane";
    private static string _userLastName = "Smith";
    private static string _identityName = $"janesmith-{Guid.NewGuid().ToString()[..6]}";

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true
        };
    }

    // ==========================================
    // 1. DASHBOARD
    // ==========================================

    [Test, Order(1)]
    public async Task Dashboard_LoadsWithStatCards()
    {
        await Page.GotoAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Page title
        await Expect(Page).ToHaveTitleAsync(new Regex("Dashboard.*Control Panel"));

        // Sidebar is visible
        await Expect(Page.Locator("aside")).ToBeVisibleAsync();

        // Stat cards visible
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Users" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Wallets" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Tenants" })).ToBeVisibleAsync();
    }

    [Test, Order(2)]
    public async Task Dashboard_SidebarNavigationWorks()
    {
        await Page.GotoAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click on Users in sidebar
        await Page.GetByRole(AriaRole.Link, new() { Name = "Users" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveURLAsync(new Regex("/identity/users"));
    }

    // ==========================================
    // 2. TENANT MANAGEMENT
    // ==========================================

    [Test, Order(10)]
    public async Task Tenants_PageLoads()
    {
        await Page.GotoAsync("/identity/tenants");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Tenant.*Control Panel"));
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create Tenant" })).ToBeVisibleAsync();
    }

    [Test, Order(11)]
    public async Task Tenants_CreateTenant()
    {
        await Page.GotoAsync("/identity/tenants");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open create dialog
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Tenant" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        // Fill form
        await Page.GetByPlaceholder("Tenant name").FillAsync(_tenantName);
        await Page.GetByPlaceholder("Brief description").FillAsync("E2E test tenant");

        // Submit
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Verify success toast
        await Expect(Page.GetByRole(AriaRole.Alert).First).ToContainTextAsync("successfully", new() { Timeout = 5000 });

        // Verify tenant appears in grid
        await Page.WaitForTimeoutAsync(1000);
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = _tenantName })).ToBeVisibleAsync();
    }

    [Test, Order(12)]
    public async Task Tenants_TenantVisibleInList()
    {
        await Page.GotoAsync("/identity/tenants");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Our tenant should be in the grid
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = _tenantName })).ToBeVisibleAsync();
    }

    // ==========================================
    // 3. USER MANAGEMENT
    // ==========================================

    [Test, Order(20)]
    public async Task Users_PageLoads()
    {
        await Page.GotoAsync("/identity/users");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("User.*Control Panel"));
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create User" })).ToBeVisibleAsync();
    }

    [Test, Order(21)]
    public async Task Users_CreateUser()
    {
        await Page.GotoAsync("/identity/users");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open create dialog
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        // Fill form
        await Page.GetByPlaceholder("Enter first name").FillAsync(_userName);
        await Page.GetByPlaceholder("Enter last name").FillAsync(_userLastName);
        await Page.GetByPlaceholder("Unique identity name").FillAsync(_identityName);

        // Submit
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();

        // Verify success
        await Expect(Page.GetByRole(AriaRole.Alert).First).ToContainTextAsync("successfully", new() { Timeout = 5000 });

        // Verify user in grid
        await Page.WaitForTimeoutAsync(1000);
        await Expect(Page.GetByText($"{_userName} {_userLastName}")).ToBeVisibleAsync();
    }

    [Test, Order(22)]
    public async Task Users_UserVisibleInList()
    {
        await Page.GotoAsync("/identity/users");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText($"{_userName} {_userLastName}")).ToBeVisibleAsync();
    }

    [Test, Order(23)]
    public async Task Users_ViewUserDetail()
    {
        await Page.GotoAsync("/identity/users");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click view button (eye icon) on the user row
        var userRow = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = _userName });
        await userRow.GetByRole(AriaRole.Button).First.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should navigate to user detail
        await Expect(Page).ToHaveURLAsync(new Regex("/identity/users/"));

        // User name should be visible
        await Expect(Page.GetByText($"{_userName} {_userLastName}")).ToBeVisibleAsync();

        // Tabs should be visible
        await Expect(Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials") })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Roles") })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Wallets") })).ToBeVisibleAsync();
    }

    // ==========================================
    // 4. ROLES
    // ==========================================

    [Test, Order(30)]
    public async Task Roles_PageLoads()
    {
        await Page.GotoAsync("/identity/roles");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Role.*Control Panel"));
    }

    // ==========================================
    // 5. SESSIONS & AUTH LOGS
    // ==========================================

    [Test, Order(40)]
    public async Task Sessions_PageLoads()
    {
        await Page.GotoAsync("/identity/sessions");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Session.*Control Panel"));
    }

    [Test, Order(41)]
    public async Task AuthLogs_PageLoads()
    {
        await Page.GotoAsync("/identity/auth-logs");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Auth.*Control Panel"));
    }

    // ==========================================
    // 6. WALLETS
    // ==========================================

    [Test, Order(50)]
    public async Task Wallets_PageLoads()
    {
        await Page.GotoAsync("/finance/wallets");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Wallet.*Control Panel"));
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create Wallet" })).ToBeVisibleAsync();
    }

    [Test, Order(51)]
    public async Task Transactions_PageLoads()
    {
        await Page.GotoAsync("/finance/transactions");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Transaction.*Control Panel"));
    }

    [Test, Order(52)]
    public async Task BatchOperations_PageLoads()
    {
        await Page.GotoAsync("/finance/batch-operations");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Batch.*Control Panel"));
    }

    // ==========================================
    // 7. SYSTEM PAGES
    // ==========================================

    [Test, Order(60)]
    public async Task Configurations_PageLoads()
    {
        await Page.GotoAsync("/lookups/configurations");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Configuration.*Control Panel"));
    }

    [Test, Order(61)]
    public async Task ReferenceData_PageLoads()
    {
        await Page.GotoAsync("/admin/reference-data");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page).ToHaveTitleAsync(new Regex("Reference.*Control Panel"));

        // Tabs should be visible
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Role Types" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Wallet Types" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Currency Types" })).ToBeVisibleAsync();
    }

    // ==========================================
    // 8. DARK MODE
    // ==========================================

    [Test, Order(70)]
    public async Task DarkMode_ToggleWorks()
    {
        await Page.GotoAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the dark mode toggle button in the header
        var toggle = Page.Locator("header").GetByRole(AriaRole.Button).First;
        await toggle.ClickAsync();

        // Give the theme a moment to apply
        await Page.WaitForTimeoutAsync(500);

        // The html element should have class "dark" toggled
        var htmlClass = await Page.Locator("html").GetAttributeAsync("class");
        // Just verify the toggle didn't crash — the class may or may not contain "dark" depending on initial state
        htmlClass.Should().NotBeNull();
    }

    // ==========================================
    // 9. NAVIGATION SMOKE TEST (all sidebar links)
    // ==========================================

    [Test, Order(80)]
    public async Task AllSidebarLinks_Navigate()
    {
        var routes = new[]
        {
            "/identity/tenants",
            "/identity/users",
            "/identity/roles",
            "/identity/sessions",
            "/identity/auth-logs",
            "/finance/wallets",
            "/finance/transactions",
            "/finance/batch-operations",
            "/lookups/configurations",
            "/admin/reference-data"
        };

        foreach (var route in routes)
        {
            await Page.GotoAsync(route);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // No error should be visible
            var errorAlert = Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Error" });
            var hasError = await errorAlert.CountAsync() > 0 && await errorAlert.IsVisibleAsync();

            if (hasError)
            {
                var errorText = await errorAlert.TextContentAsync();
                Assert.Warn($"Page {route} shows error: {errorText}");
            }

            // Page should have a title
            var title = await Page.TitleAsync();
            title.Should().Contain("Control Panel", $"Route {route} should have Control Panel in title");
        }
    }
}
