using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using MemoryPack;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using XFramework.Portal.Features.POS;
using XFramework.Portal.Features.POS.Receipts;

namespace Portal.E2ETests;

[TestFixture, Category("Area:PortalContract"), Category("Module:POS")]
public sealed class PosReceiptTests
{
    [TestCase(58, PosReceiptTemplate.Classic)]
    [TestCase(80, PosReceiptTemplate.Classic)]
    [TestCase(58, PosReceiptTemplate.Compact)]
    [TestCase(80, PosReceiptTemplate.Compact)]
    [TestCase(58, PosReceiptTemplate.Bold)]
    [TestCase(80, PosReceiptTemplate.Bold)]
    public void Document_ConfiguredWidthAndTemplate_UsesOnlyCustomerReceiptFields(int width, PosReceiptTemplate template)
    {
        var settings = new PosReceiptSettings { PaperWidthMm = width, Template = template };
        var html = PosReceiptDocument.Create(Sale(), settings, "Example merchant", "USD");
        html.Should().Contain($"size:{width}mm").And.Contain($"body class=\"{template.ToString().ToLowerInvariant()}\"")
            .And.Contain("Example merchant").And.Contain("SALE-001").And.Contain("Coffee")
            .And.Contain("USD 12.00").And.Contain("Cash received").And.Contain("Change")
            .And.NotContain("AUDIT-SECRET").And.NotContain("INTERNAL-REFERENCE").And.NotContain("PaymentPending");
        html.Should().NotContain(SaleId.ToString());
    }

    [Test]
    public void Document_TextIsEncoded_AndSectionVisibilityAndOrderAreApplied()
    {
        var settings = new PosReceiptSettings { MerchantName = "<script>alert(1)</script>", Header = "<img onerror=alert(2)>", Footer = "Hidden footer" };
        settings.SectionOrder.Reverse();
        settings.HiddenSections.Add(PosReceiptSection.Footer);
        var html = PosReceiptDocument.Create(Sale(), settings, "fallback", "USD");
        html.Should().Contain("&lt;script&gt;").And.Contain("&lt;img onerror=alert(2)&gt;").And.NotContain("<script>").And.NotContain("Hidden footer");
        html.IndexOf("data-section=\"Totals\"", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("data-section=\"Items\"", StringComparison.Ordinal));
    }

    [Test]
    public void Document_PendingPayment_DoesNotClaimPaidOrIncludeFailureDetails()
    {
        var sale = Sale() with { Status = PosSaleStatus.PaymentPending, Payments = [new() { Status = PosPaymentStatus.Pending, Amount = 12, FailureReason = "AUDIT-SECRET" }] };
        var html = PosReceiptDocument.Create(sale, new(), "Store", "USD");
        html.Should().Contain("Payment pending").And.NotContain("Paid (").And.NotContain("AUDIT-SECRET");
    }

    [TestCase("width")]
    [TestCase("template")]
    [TestCase("duplicate")]
    [TestCase("missing")]
    [TestCase("hide-required")]
    [TestCase("header")]
    [TestCase("remote-logo")]
    [TestCase("svg-logo")]
    [TestCase("fake-logo")]
    public void Settings_InvalidConfiguration_IsRejected(string invalid)
    {
        var settings = new PosReceiptSettings();
        switch (invalid)
        {
            case "width": settings.PaperWidthMm = 100; break;
            case "template": settings.Template = (PosReceiptTemplate)99; break;
            case "duplicate": settings.SectionOrder[0] = PosReceiptSection.Items; break;
            case "missing": settings.SectionOrder.Remove(PosReceiptSection.Totals); break;
            case "hide-required": settings.HiddenSections.Add(PosReceiptSection.Totals); break;
            case "header": settings.Header = new string('a', 501); break;
            case "remote-logo": settings.LogoDataUrl = "https://example.com/logo.png"; break;
            case "svg-logo": settings.LogoDataUrl = "data:image/svg+xml;base64,PHN2Zy8+"; break;
            case "fake-logo": settings.LogoDataUrl = "data:image/png;base64,SGVsbG8="; break;
        }
        settings.IsValid().Should().BeFalse();
        new PosCheckoutSettings { Receipt = settings }.IsValid().Should().BeFalse();
        var act = () => PosReceiptDocument.Create(Sale(), settings, "Store", "USD");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Settings_LegacyJsonGetsDefaults_AndDraftCloneDoesNotMutateSavedSettings()
    {
        var settings = JsonSerializer.Deserialize<PosCheckoutSettings>("{\"TableTrackingEnabled\":true,\"CheckoutFields\":[]}")!;
        settings.Receipt.IsValid().Should().BeTrue();
        settings.Receipt.PaperWidthMm.Should().Be(80);
        var clone = settings.DeepClone();
        clone.Receipt.SectionOrder.Reverse();
        clone.Receipt.HiddenSections.Add(PosReceiptSection.Footer);
        clone.Receipt.MerchantName = "Draft";
        settings.Receipt.SectionOrder[0].Should().Be(PosReceiptSection.Header);
        settings.Receipt.HiddenSections.Should().BeEmpty();
        settings.Receipt.MerchantName.Should().BeEmpty();
        JsonSerializer.Deserialize<PosCheckoutSettings>(JsonSerializer.Serialize(clone))!.Receipt.Should().BeEquivalentTo(clone.Receipt);
        MemoryPackSerializer.Deserialize<PosCheckoutSettings>(MemoryPackSerializer.Serialize(clone))!.Receipt.Should().BeEquivalentTo(clone.Receipt);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Editor_SectionChanges_UpdateReceiptDraftUnlessReadOnly(bool disabled)
    {
        var settings = new PosReceiptSettings();
        var editor = new ReceiptSettingsEditor { Settings = settings, Disabled = disabled };
        var order = settings.SectionOrder.ToList();
        (order[0], order[1]) = (order[1], order[0]);
        await Invoke(editor, "SetSectionOrder", order);
        await Invoke(editor, "SetHiddenSections", new List<PosReceiptSection> { PosReceiptSection.Footer });
        settings.SectionOrder.IndexOf(PosReceiptSection.Header).Should().Be(disabled ? 0 : 1);
        settings.HiddenSections.Contains(PosReceiptSection.Footer).Should().Be(!disabled);
        settings.HiddenSections.Should().NotContain(PosReceiptSection.Totals);
        settings.IsValid().Should().BeTrue();
    }

    private static Task Invoke(object target, string method, params object[] args) =>
        (Task)target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;

    internal static readonly Guid SaleId = Guid.NewGuid();
    internal static PosSaleReceiptResponse Sale() => new()
    {
        Id = SaleId, TenantId = Guid.NewGuid(), SaleNumber = "SALE-001", Status = PosSaleStatus.Completed,
        IdempotencyKey = "AUDIT-SECRET", FailureReason = "AUDIT-SECRET", RecoveryState = "AUDIT-SECRET",
        CompletedAt = new DateTime(2026, 10, 22, 9, 30, 0, DateTimeKind.Utc), SubtotalAmount = 12, TotalAmount = 12,
        Lines = [new() { Id = Guid.NewGuid(), ProductName = "Coffee", VariantName = "Large", Quantity = 2, UnitPrice = 6, LineTotal = 12, FailureReason = "AUDIT-SECRET" }],
        Payments = [new() { Method = PosPaymentMethod.CashDrawer, Status = PosPaymentStatus.Captured, Amount = 12, CashTenderedAmount = 20, ChangeAmount = 8, ReferenceNumber = "INTERNAL-REFERENCE", IdempotencyKey = "AUDIT-SECRET" }]
    };
}
