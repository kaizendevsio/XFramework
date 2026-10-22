using FluentAssertions;
using Microsoft.Playwright;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    [TestCase(390, 844, false)]
    [TestCase(1920, 1080, false)]
    [TestCase(390, 844, true)]
    [TestCase(1920, 1080, true)]
    public async Task MovementOptions_DescribeEffectsAndKeepReadableSelection(int width, int height, bool dark)
    {
        PrepareProductWorkflow();
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference });
        await Page.EvaluateAsync("dark => localStorage.setItem('bb-theme', JSON.stringify({ baseColor: 'slate', primaryColor: 'blue', radius: 0.5, isDarkMode: dark }))", dark);
        await OpenWorkflowProduct();
        await WorkflowAvailableStock.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post Stock Movement", Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Post Stock Movement", Exact = true });
        var trigger = dialog.GetByRole(AriaRole.Combobox, new() { Name = "Movement Type", Exact = true });
        await Expect(trigger).ToContainTextAsync("Adjustment");
        await trigger.ClickAsync();
        await Expect(Page.GetByPlaceholder("Search movement types...")).ToBeFocusedAsync();
        var choices = new Dictionary<string, string>
        {
            ["Opening balance"] = "does not replace the balance",
            ["Receipt"] = "Adds received stock",
            ["Adjustment"] = "positive or negative quantity",
            ["Transfer"] = "Requires a source and destination; unavailable in this dialog.",
            ["Reservation"] = "Increases reserved stock and reduces available stock",
            ["Release"] = "Reduces reserved stock and increases available stock",
            ["Shipment"] = "does not release reserved stock",
            ["Return"] = "Adds returned stock"
        };
        foreach (var (name, description) in choices)
        {
            var option = Page.GetByRole(AriaRole.Option).Filter(new() { HasText = description });
            await Expect(option).ToContainTextAsync(name);
            await Expect(option.Locator(".movement-option-description")).ToContainTextAsync(description);
            (await option.Locator(".movement-option").EvaluateAsync<bool>("element => { const [label, description] = element.children; return description.getBoundingClientRect().top >= label.getBoundingClientRect().bottom; }")).Should().BeTrue();
        }
        await Expect(Page.GetByRole(AriaRole.Option).Filter(new() { HasText = "Requires a source and destination" })).ToHaveAttributeAsync("aria-disabled", "true");
        var opening = Page.GetByRole(AriaRole.Option).Filter(new() { HasText = "does not replace the balance" });
        await opening.ScrollIntoViewIfNeededAsync();
        await Screenshot($"movement-options-{width}-{dark}");
        await opening.ClickAsync();
        await Expect(trigger).ToHaveTextAsync("Opening balance");
        await Expect(trigger).ToBeFocusedAsync();
        await Expect(dialog).ToBeVisibleAsync();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await trigger.ClickAsync();
            await Expect(opening).ToBeVisibleAsync();
            await Expect(Page.GetByPlaceholder("Search movement types...")).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Escape");
            await Expect(Page.GetByRole(AriaRole.Option)).ToHaveCountAsync(0);
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(trigger).ToBeFocusedAsync();
            await trigger.ClickAsync();
            var next = cycle % 2 == 0 ? "Receipt" : "Opening balance";
            await Page.GetByRole(AriaRole.Option).Filter(new() { HasText = choices[next] }).ClickAsync();
            await Expect(trigger).ToHaveTextAsync(next);
            await Expect(trigger).ToBeFocusedAsync();
            await Expect(dialog).ToBeVisibleAsync();
        }
        await Screenshot($"movement-options-reopened-{width}-{dark}");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post Stock Movement", Exact = true }).ClickAsync();
        await Expect(trigger).ToHaveTextAsync("Adjustment");
        await trigger.ClickAsync();
        await Expect(Page.GetByPlaceholder("Search movement types...")).ToBeFocusedAsync();
        await Screenshot($"movement-options-dialog-reopened-{width}-{dark}");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");
        var search = Page.GetByPlaceholder("Search movement types...");
        await Expect(search).ToBeFocusedAsync();
        await search.FillAsync("Receipt");
        await Expect(Page.GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(trigger).ToHaveTextAsync("Receipt");
        await Expect(trigger).ToBeFocusedAsync();
        await Expect(dialog).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }
}
