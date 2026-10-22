using FluentAssertions;
using Microsoft.Playwright;
using POS.Domain.Shared.Contracts;

namespace Portal.E2ETests;

public sealed partial class PosScannerPairingDialogE2ETests
{
    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    [TestCase(390, 844)]
    public async Task ReceiptSettings_EditorPreviewAndSave_PreserveBoundedLayout(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.AddInitScriptAsync("""
            const receiptListener = EventTarget.prototype.addEventListener;
            EventTarget.prototype.addEventListener = function(type, listener, options) {
                const result = receiptListener.call(this, type, listener, options);
                if (type === 'change' && this instanceof HTMLInputElement && this.getAttribute('aria-label') === 'Receipt merchant name')
                    this.dataset.receiptReady = 'true';
                return result;
            };
            """);
        await Page.GotoAsync(app.Urls.Single() + "/pos-settings-fixture");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Receipts", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".document-sections li").First).ToHaveCSSAsync("display", "grid");
        await Expect(Page.GetByLabel("Receipt merchant name", new() { Exact = true })).ToHaveAttributeAsync("data-receipt-ready", "true");
        await Page.GetByLabel("Receipt merchant name", new() { Exact = true }).FillAsync("North Market");
        await Page.GetByLabel("Receipt merchant name", new() { Exact = true }).BlurAsync();
        await Page.GetByLabel("Receipt paper width", new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "58 mm", Exact = true }).ClickAsync();
        await Page.GetByLabel("Receipt template", new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Bold", Exact = true }).ClickAsync();
        await Page.GetByLabel("Receipt header", new() { Exact = true }).FillAsync("123 Market Street");
        await Page.GetByLabel("Receipt header", new() { Exact = true }).BlurAsync();
        var image = await Page.EvaluateAsync<string>("""
            () => {const c=document.createElement('canvas'); c.width=200; c.height=60;
                const x=c.getContext('2d'); x.fillStyle='white'; x.fillRect(0,0,200,60);
                x.fillStyle='black'; x.font='bold 30px Arial'; x.fillText('NORTH',40,40); return c.toDataURL('image/png').split(',')[1];}
            """);
        await Page.GetByLabel("Receipt logo", new() { Exact = true }).SetInputFilesAsync(new FilePayload
        { Name = "north.png", MimeType = "image/png", Buffer = Convert.FromBase64String(image) });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Remove receipt logo", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Move Header down", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Switch, new() { Name = "Show receipt Footer", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Switch, new() { Name = "Show receipt Totals", Exact = true })).ToBeDisabledAsync();
        var preview = Page.FrameLocator("iframe[title='Receipt preview']");
        await Expect(preview.Locator("h1")).ToHaveTextAsync("North Market");
        await Expect(preview.Locator("body")).ToHaveClassAsync("bold");
        await Expect(preview.Locator("[data-section='Header']")).ToHaveTextAsync("123 Market Street");
        await Expect(preview.Locator("[data-section='Footer']")).ToHaveCountAsync(0);
        await Expect(preview.Locator(".logo")).ToBeVisibleAsync();
        (await preview.Locator("body").BoundingBoxAsync())!.Width.Should().BeApproximately(58 * 96f / 25.4f, 1f);
        state.CheckoutSettings.Receipt.MerchantName.Should().BeEmpty("the editor must mutate a draft, not the saved response");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Wait(() => Volatile.Read(ref state.SettingsSaves) == 1);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeEnabledAsync();
        await Expect(Page.Locator(".pos-settings-page").GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        state.SettingsSaves.Should().Be(1);
        state.CheckoutSettings.Receipt.PaperWidthMm.Should().Be(58);
        state.CheckoutSettings.Receipt.Template.Should().Be(PosReceiptTemplate.Bold);
        state.CheckoutSettings.Receipt.SectionOrder[0].Should().Be(PosReceiptSection.Sale);
        state.CheckoutSettings.Receipt.HiddenSections.Should().Contain(PosReceiptSection.Footer);
        state.CheckoutSettings.Receipt.LogoDataUrl.Should().StartWith("data:image/png;base64,");
        await CheckoutScreenshot("receipt-settings", width);
        await Page.GetByLabel("Receipt merchant name", new() { Exact = true }).FillAsync("Unsaved name");
        await Page.GetByLabel("Receipt merchant name", new() { Exact = true }).BlurAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Receipt merchant name", new() { Exact = true })).ToHaveValueAsync("North Market");
    }
}
