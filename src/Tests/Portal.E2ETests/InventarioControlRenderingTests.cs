using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Features.Inventario;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class InventarioControlRenderingTests
{
    [TestCase("SUP")]
    [TestCase("Preferred supplier")]
    [TestCase("SUP - Preferred supplier")]
    public void PlanningDraft_ResolvesPreferredSupplierWithoutScopingOrderList(string preference)
    {
        var supplier = new XFramework.Inventario.Domain.Shared.Contracts.Supplier
        {
            Id = Guid.NewGuid(), Code = "SUP", Name = "Preferred supplier", IsActive = true
        };
        var page = new XFramework.Portal.Features.Inventario.Pages.PurchaseOrders { PreferredSupplier = preference };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(XFramework.Portal.Features.Inventario.Pages.PurchaseOrders).GetField("_suppliers", flags)!
            .SetValue(page, new List<XFramework.Inventario.Domain.Shared.Contracts.Supplier> { supplier });
        typeof(XFramework.Portal.Features.Inventario.Pages.PurchaseOrders).GetMethod("ResolvePreferredSupplier", flags)!
            .Invoke(page, null).Should().Be(supplier.Id.ToString());
        page.SupplierId.Should().BeNull("draft preference must not filter the purchase-order list");
    }

    [Test]
    public void TemplateColumn_ClientFilter_UsesDisplayedLookupValue()
    {
        var labels = new Dictionary<string, string> { ["product-id"] = "E2E Shirt" };
        var column = new BbDataGridTemplateColumn<PickerItem>
        {
            Title = "Product", Filterable = true,
            FilterBy = item => labels[item.Id],
            SortBy = item => labels[item.Id]
        };
        var filterColumn = (BlazorBlueprint.Primitives.DataGrid.IDataGridColumn<PickerItem>)column;
        filterColumn.GetSortAndFilterValue(new("product-id", "unused"))
            .Should().Be("E2E Shirt", "client filtering reads the sort selector rather than FilterBy alone");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EntityPicker_RendersOneNamedButtonAndPreservesDisabledState(bool disabled)
    {
        var html = await Render<XfEntityPicker<PickerItem>>(new()
        {
            ["Label"] = "Product", ["Disabled"] = disabled,
            ["Items"] = new List<PickerItem> { new("one", "First product") },
            ["Value"] = "one",
            ["ValueSelector"] = (Func<PickerItem, string>)(x => x.Id),
            ["TextSelector"] = (Func<PickerItem, string>)(x => x.Name)
        });
        Regex.Matches(html, "<button\\b").Count.Should().Be(1, "the picker must have one focus target, without nested buttons");
        var button = Regex.Match(html, "<button\\b[^>]*>").Value;
        button.Should().Contain("aria-label=\"Product\"");
        button.Should().Contain("xf-entity-picker-trigger");
        button.Contains("disabled").Should().Be(disabled);
        html.Should().Contain("First product");
    }

    [Test]
    public async Task Select_ExplicitDisplayAndName_RenderBeforeOpeningOptions()
    {
        var html = await Render<InventorySelect<string>>(new()
        {
            ["Label"] = "Variant", ["Value"] = "base",
            ["DisplayTextSelector"] = (Func<string, string>)(_ => "Base product")
        });
        html.Should().Contain("Base product");
        html.Should().Contain("aria-label=\"Variant\"");
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, NoBrowserJavaScript>();
        services.AddBlazorBlueprintComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    public sealed record PickerItem(string Id, string Name);

    private sealed class NoBrowserJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static component rendering must not invoke browser JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
