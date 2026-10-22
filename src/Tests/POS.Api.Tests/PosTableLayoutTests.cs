using System.Text.Json;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;

namespace POS.Api.Tests;

[TestFixture]
public sealed class PosTableLayoutTests
{
    private static PosCheckoutSettings Settings()
    {
        var layout = new PosTableLayout { Tables = [new() { Label = "A1" }] };
        return new() { TableTrackingEnabled = true, ActiveTableLayoutId = layout.Id, TableLayouts = [layout] };
    }

    [Test]
    public void Validate_HoldingRequiresTable_OptionalPaymentDoesNot()
    {
        var settings = Settings();
        settings.Validate(new(), false, requireTable: true).Should().Contain("Select a table");
        settings.Validate(new(), true).Should().BeNull();
        settings.TableRequired = true;
        settings.Validate(new(), true).Should().Contain("Select a table");
    }

    [Test]
    public void Validate_OnlyCurrentLayoutTableIdsAccepted()
    {
        var settings = Settings();
        var layout = settings.ActiveTableLayout!;
        var metadata = new PosCheckoutMetadata { TableLayoutId = layout.Id, TableId = layout.Tables[0].Id };
        settings.Validate(metadata, false, true).Should().BeNull();
        metadata.TableId = Guid.NewGuid();
        settings.Validate(metadata, false, true).Should().NotBeNull();
        metadata.TableId = layout.Tables[0].Id;
        metadata.TableLayoutId = Guid.NewGuid();
        settings.Validate(metadata, false, true).Should().NotBeNull();
    }

    [Test]
    public void Validate_DisabledTablesRejectIdsEvenWithoutLabels()
    {
        new PosCheckoutSettings().Validate(new() { TableId = Guid.NewGuid() }, false).Should().Be("Table tracking is disabled");
    }

    [Test]
    public void Layout_InvalidNamesBoundsOverlapAndDuplicatesAreRejected()
    {
        var layout = Settings().ActiveTableLayout!;
        layout.IsValid().Should().BeTrue();
        layout.Tables.Add(new() { Label = "A2" });
        layout.IsValid().Should().BeFalse();
        layout.Tables[1].Column = 4;
        layout.IsValid().Should().BeTrue();
        layout.Tables[1].Label = " a1 ";
        layout.IsValid().Should().BeFalse();
        layout.Tables[1].Label = "A2";
        layout.Tables[1].Column = 12;
        layout.IsValid().Should().BeFalse();
        layout.Tables[1].Column = int.MaxValue;
        layout.IsValid().Should().BeFalse();
    }

    [Test]
    public void Settings_JsonAndDeepClone_PreserveLayoutsWithoutSharingMutableData()
    {
        var original = Settings();
        var saved = JsonSerializer.Deserialize<PosCheckoutSettings>(JsonSerializer.Serialize(original))!;
        saved.Should().BeEquivalentTo(original);
        var copy = saved.DeepClone();
        copy.TableLayouts[0].Tables[0].Label = "Changed";
        saved.TableLayouts[0].Tables[0].Label.Should().Be("A1");
    }

    [Test]
    public void Hash_TableIdentityChanges_AreNotTreatedAsSamePayment()
    {
        var request = new CheckoutPosSaleRequest { CheckoutMetadata = new() { TableNumber = "A1" } };
        var legacy = PosServiceHelpers.BuildSaleRequestHash(request);
        request.CheckoutMetadata.TableId = Guid.NewGuid();
        request.CheckoutMetadata.TableLayoutId = Guid.NewGuid();
        var selected = PosServiceHelpers.BuildSaleRequestHash(request);
        selected.Should().NotBe(legacy);
        request.CheckoutMetadata.TableId = Guid.NewGuid();
        PosServiceHelpers.BuildSaleRequestHash(request).Should().NotBe(selected);
    }
}
