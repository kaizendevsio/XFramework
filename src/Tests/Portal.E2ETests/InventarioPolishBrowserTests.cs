using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Polish_ProductVariations_OnlyShowSelectedGrid(int width, int height)
    {
        _state.VariationsOnly = true;
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-variations-polish-fixture");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Variation types", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Grid)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "SIZE", Exact = true })).ToBeVisibleAsync();
        await Screenshot($"polish-variation-types-{width}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Variants", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Grid)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "Large", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Variation Type", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Variant", Exact = true })).ToBeVisibleAsync();
        await Screenshot($"polish-variants-{width}");
        _errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Polish_ReorderRule_OnlyQueriesPresentScopeIds(bool scoped)
    {
        _state.PolishRuleScoped = scoped;
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Name = "Rule warehouse", Code = "WH" };
        _state.Warehouses.Add(warehouse);
        _state.Locations.Add(new InventoryLocation { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = warehouse.Id, Name = "Rule location", Code = "BIN" });
        _data.Invocations.Clear();
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-rule-polish-fixture");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reorder Rule Summary", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Synthetic QA product", Exact = true })).ToBeVisibleAsync();
        var scope = Page.Locator("main p").Filter(new() { HasText = scoped ? "Size: Large" : "Base product" });
        await Expect(scope).ToContainTextAsync(scoped ? "Rule warehouse" : "Any warehouse");
        await Expect(scope).ToContainTextAsync(scoped ? "Rule location" : "Any location");
        _data.Verify(x => x.Query<ProductVariation>(), scoped ? Times.Once() : Times.Never());
        _data.Verify(x => x.Query<Warehouse>(), scoped ? Times.Once() : Times.Never());
        _data.Verify(x => x.Query<InventoryLocation>(), scoped ? Times.Once() : Times.Never());
        await Expect(Page.GetByText("Could not load record.", new() { Exact = true })).ToHaveCountAsync(0);
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Polish_SupplierActionsAndHistoryTabs_StayUsable(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-supplier-polish-fixture");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Back to suppliers" })).ToBeVisibleAsync();
        var actions = Page.Locator(".supplier-actions");
        await Expect(actions.GetByRole(AriaRole.Button, new() { Name = "Edit details" })).ToBeVisibleAsync();
        var readBounds = (await actions.BoundingBoxAsync())!;
        var editBounds = (await actions.GetByRole(AriaRole.Button).BoundingBoxAsync())!;
        Math.Abs(readBounds.X + readBounds.Width - editBounds.X - editBounds.Width).Should().BeLessThan(2);
        await Screenshot($"polish-supplier-view-{width}");
        await actions.GetByRole(AriaRole.Button, new() { Name = "Edit details" }).ClickAsync();
        await Expect(Page.GetByLabel("Name", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GetByLabel("Name", new() { Exact = true }).FillAsync("Unsaved name");
        var save = actions.GetByRole(AriaRole.Button, new() { Name = "Save changes" });
        await Expect(save).ToBeVisibleAsync();
        var saveBounds = (await save.BoundingBoxAsync())!;
        Math.Abs(readBounds.X + readBounds.Width - saveBounds.X - saveBounds.Width).Should().BeLessThan(2);
        await Screenshot($"polish-supplier-edit-{width}");
        await actions.GetByRole(AriaRole.Button, new() { Name = "Discard" }).ClickAsync();
        await Expect(Page.GetByText("Unsaved name", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "PO-001", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "PO-011", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Go to last page", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "PO-523", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "REC-001", Exact = true })).ToBeVisibleAsync();
        await Screenshot($"polish-supplier-receipts-{width}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Product movements", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "Synthetic QA product", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Filter Product", Exact = true })).ToBeVisibleAsync();
        await Screenshot($"polish-supplier-movements-{width}");
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Polish_SupplierDeclinedReceipts_DoNotBlockOrdersAndCanRetry(int width, int height)
    {
        _state.DenyReceipts = true;
        _state.MovementsEnabled = false;
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-supplier-polish-fixture");
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "PO-001", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Product movements", Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("This history tab could not be loaded");
        await Expect(Page.GetByText("No receipts for this supplier.", new() { Exact = true })).ToHaveCountAsync(0);
        await Screenshot($"polish-supplier-receipts-declined-{width}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Purchase orders", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "PO-001", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Retry receipts", Exact = true })).ToBeVisibleAsync();
        _state.DenyReceipts = false;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Retry receipts", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Gridcell, new() { Name = "REC-001", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Polish_OrderAndLocation_OnlyShowSelectedGrid(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-order-polish-fixture");
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Order lines", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
        await Screenshot($"polish-order-lines-{width}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receiving documents", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "View receipt: REC-001", Exact = true })).ToBeVisibleAsync();
        await Screenshot($"polish-order-receipts-{width}");
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, Name = "Fixture warehouse", Code = "WH" };
        _state.Warehouses.Add(warehouse);
        _state.Locations.Add(new InventoryLocation { Id = Guid.NewGuid(), TenantId = _state.ActiveTenant, WarehouseId = warehouse.Id, Code = "BIN", Name = "Fixture location" });
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-location-polish-fixture");
        foreach (var tab in new[] { "Balances", "Movements", "Reservations" })
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = tab, Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Tabpanel)).ToHaveCountAsync(1);
            await Screenshot($"polish-location-{tab.ToLowerInvariant()}-{width}");
        }
        _errors.Should().BeEmpty();
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task Polish_Settings_HasSpacedFooterAndShortcutButtons(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-settings-fixture");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Inventario Settings", Exact = true })).ToBeVisibleAsync();
        var configuration = Page.GetByRole(AriaRole.Navigation, new() { Name = "Inventario configuration" });
        await Expect(configuration.GetByRole(AriaRole.Button, new() { Name = "Suppliers", Exact = true })).ToBeVisibleAsync();
        await Expect(configuration.GetByRole(AriaRole.Link)).ToHaveCountAsync(0);
        var lastSection = Page.Locator(".settings-section").Last;
        (await lastSection.EvaluateAsync<string>("e => getComputedStyle(e).borderBottomWidth")).Should().Be("0px");
        var section = (await lastSection.BoundingBoxAsync())!;
        var save = (await Page.GetByRole(AriaRole.Button, new() { Name = "Save Settings", Exact = true }).BoundingBoxAsync())!;
        (save.Y - section.Y - section.Height).Should().BeGreaterThanOrEqualTo(15);
        await Screenshot($"polish-settings-{width}");
        _errors.Should().BeEmpty();
    }
}
