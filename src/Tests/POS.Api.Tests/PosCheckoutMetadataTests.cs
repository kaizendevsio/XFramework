using System.Text.Json;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;

namespace POS.Api.Tests;

[TestFixture]
public sealed class PosCheckoutMetadataTests
{
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public void Validate_RequiredFields_OnlyRequiredWhenEnabledAtPayment(bool enabled, bool paying, bool invalid)
    {
        var settings = Settings(enabled);
        (settings.Validate(new(), paying) is not null).Should().Be(invalid);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_BlankRequiredValue_IsRejected(string value) =>
        Settings().Validate(new() { Fields = new() { ["coupon"] = value } }, true).Should().Be("Coupon is required");

    [TestCase(false, "12", true)]
    [TestCase(false, null, false)]
    [TestCase(true, "12", true)]
    public void Validate_TableToggle_RejectsDisabledTableData(bool enabled, string? table, bool invalid) =>
        (new PosCheckoutSettings { TableTrackingEnabled = enabled }.Validate(new() { TableNumber = table }, true) is not null)
        .Should().Be(invalid);

    [TestCase("unknown", true)]
    [TestCase("coupon", false)]
    public void Validate_UnknownOrDisabledField_IsRejected(string key, bool enabled) =>
        Settings(enabled).Validate(new() { Fields = new() { [key] = "VALUE" } }, false)
            .Should().Contain("unknown or disabled");

    [Test]
    public void Validate_OverlongValuesAndDuplicateKeys_AreRejected()
    {
        Settings().Validate(new() { Fields = new() { ["coupon"] = new string('a', 501) } }, true)
            .Should().Contain("500");
        var settings = Settings();
        settings.CheckoutFields.Add(settings.CheckoutFields[0] with { });
        settings.IsValid().Should().BeFalse();
    }

    [Test]
    public void ReceiptAndHeldCart_MetadataRoundTrips_WithoutChangingMoney()
    {
        var metadata = new PosCheckoutMetadata { TableNumber = "12",
            Fields = new() { ["coupon"] = "SAVE50" }, Labels = new() { ["coupon"] = "Coupon" } };
        var json = JsonSerializer.Serialize(metadata);
        var sale = new PosSale { CheckoutMetadataJson = json, TotalAmount = 100, DiscountAmount = 0 };
        var cart = new PosCart { CheckoutMetadataJson = json };
        PosServiceHelpers.ToSaleReceiptResponse(sale).CheckoutMetadata.Should().BeEquivalentTo(metadata);
        PosServiceHelpers.ToCartResponse(cart).CheckoutMetadata.Should().BeEquivalentTo(metadata);
        PosServiceHelpers.ToCartSummaryResponse(cart).TableNumber.Should().Be("12");
        sale.TotalAmount.Should().Be(100);
        sale.DiscountAmount.Should().Be(0);
    }

    [Test]
    public void RequestHash_IncludesMetadata_ButIgnoresClientLabelsAndDictionaryOrder()
    {
        var request = new CheckoutPosSaleRequest();
        var emptyHash = PosServiceHelpers.BuildSaleRequestHash(request);
        request.CheckoutMetadata = new() { Fields = new() { ["coupon"] = "A", ["order"] = "B" } };
        var hash = PosServiceHelpers.BuildSaleRequestHash(request);
        hash.Should().NotBe(emptyHash);
        request.CheckoutMetadata.Fields = new() { ["order"] = "B", ["coupon"] = "A" };
        request.CheckoutMetadata.Labels["coupon"] = "Forged";
        PosServiceHelpers.BuildSaleRequestHash(request).Should().Be(hash);
        request.CheckoutMetadata.TableNumber = "1";
        PosServiceHelpers.BuildSaleRequestHash(request).Should().NotBe(hash);
    }

    private static PosCheckoutSettings Settings(bool enabled = true) => new()
    {
        CheckoutFields = [new() { Key = "coupon", Label = "Coupon", Enabled = enabled, Required = true }]
    };
}
