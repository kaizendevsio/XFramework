using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.Playwright;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    [TestCase(false)]
    [TestCase(true)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_Combination_CreateEditPreservesOptionsAndExplicitZeroPrice(bool createFree)
    {
        PrepareProductWorkflow();
        _state.CombinationWorkflow = true;
        var createPrice = createFree ? 0m : 23.50m;
        var editPrice = createFree ? 31.25m : 0m;
        var scenario = createFree ? "create-free" : "edit-free";
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-variations-polish-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Variant", Exact = true }).ClickAsync();
        var create = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add Variant", Exact = true });
        await create.GetByRole(AriaRole.Switch, new() { Name = "Combination of options", Exact = true }).ClickAsync();
        await create.GetByLabel("Combination name", new() { Exact = true }).FillAsync("Red / Large");
        await CombinationPrice(create).FillAsync(createPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

        var pickers = create.GetByRole(AriaRole.Button, new() { Name = "Option type", Exact = true });
        await pickers.Nth(0).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Color (Tenant-wide)", Exact = true }).ClickAsync();
        await create.GetByLabel("Option value", new() { Exact = true }).Nth(0).FillAsync("Red");
        await pickers.Nth(1).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Option, new() { Name = "Color (Tenant-wide)", Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Option, new() { Name = "Size (Product-local)", Exact = true }).ClickAsync();
        await create.GetByLabel("Option value", new() { Exact = true }).Nth(1).FillAsync("Large");
        await create.GetByLabel("Combination name", new() { Exact = true }).ClickAsync();
        await Expect(pickers.Nth(0)).ToContainTextAsync("Color (Tenant-wide)");
        await Expect(pickers.Nth(1)).ToContainTextAsync("Size (Product-local)");
        _state.CombinationCreates.Should().BeEmpty();
        await Screenshot($"product-combination-{scenario}-create-1920");

        await create.GetByRole(AriaRole.Button, new() { Name = "Create Variant", Exact = true }).ClickAsync();
        await Expect(create).ToBeHiddenAsync();
        var created = _state.CombinationCreates.Should().ContainSingle().Subject;
        created.Metadata.RequestedTenantId.Should().Be(_state.ActiveTenant);
        created.ProductId.Should().Be(_state.PolishProductId);
        created.ProductVariationTypeId.Should().Be(Guid.Empty);
        created.Name.Should().Be("Red / Large");
        created.Price.Should().Be(createPrice);
        created.CombinationOptions.Should().BeEquivalentTo(new[]
        {
            new ProductVariationOption { ProductVariationTypeId = _state.CombinationColorId, Value = "Red" },
            new ProductVariationOption { ProductVariationTypeId = _state.CombinationSizeId, Value = "Large" }
        });
        var persisted = _state.CombinationVariations.Should().ContainSingle().Subject;
        var variantId = persisted.Id;
        var stamp = persisted.ConcurrencyStamp;

        await OpenCombinationEdit("Red / Large");
        var edit = Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Variant", Exact = true });
        await Expect(edit.GetByRole(AriaRole.Switch, new() { Name = "Combination of options", Exact = true })).ToBeDisabledAsync();
        await AssertCombinationFields(edit, "Red / Large", "Red", createPrice);
        await edit.GetByLabel("Combination name", new() { Exact = true }).FillAsync("Blue / Large");
        await edit.GetByLabel("Option value", new() { Exact = true }).Nth(0).FillAsync("Blue");
        await CombinationPrice(edit).FillAsync(editPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        await edit.GetByLabel("Combination name", new() { Exact = true }).ClickAsync();
        await Screenshot($"product-combination-{scenario}-edit-1920");
        await edit.GetByRole(AriaRole.Button, new() { Name = "Save Variant", Exact = true }).ClickAsync();
        await Expect(edit).ToBeHiddenAsync();

        var updated = _state.CombinationUpdates.Should().ContainSingle().Subject;
        updated.Metadata.RequestedTenantId.Should().Be(_state.ActiveTenant);
        updated.ProductVariationId.Should().Be(variantId);
        updated.ExpectedConcurrencyStamp.Should().Be(stamp);
        updated.ProductVariationTypeId.Should().Be(Guid.Empty);
        updated.Name.Should().Be("Blue / Large");
        updated.Price.Should().Be(editPrice);
        updated.CombinationOptions.Should().BeEquivalentTo(new[]
        {
            new ProductVariationOption { ProductVariationTypeId = _state.CombinationColorId, Value = "Blue" },
            new ProductVariationOption { ProductVariationTypeId = _state.CombinationSizeId, Value = "Large" }
        });
        created.CombinationOptions![0].Value.Should().Be("Red", "editing must not mutate the previously submitted options");
        _state.CombinationVariations.Should().ContainSingle().Which.Id.Should().Be(variantId);

        await OpenCombinationEdit("Blue / Large");
        await AssertCombinationFields(edit, "Blue / Large", "Blue", editPrice);
        await edit.GetByLabel("Option value", new() { Exact = true }).Nth(0).FillAsync("Unsaved");
        await edit.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await OpenCombinationEdit("Blue / Large");
        await AssertCombinationFields(edit, "Blue / Large", "Blue", editPrice);
        await edit.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        _state.CombinationUpdates.Should().ContainSingle();
        _state.BasicStockRequests.Should().BeEmpty();
        _data.Verify(x => x.Add(It.IsAny<ProductVariation>()), Times.Never());
        _data.Verify(x => x.Update(It.IsAny<ProductVariation>()), Times.Never());
        _data.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
        _errors.Should().BeEmpty();
    }

    private static ILocator CombinationPrice(ILocator dialog) =>
        dialog.GetByLabel("Amount in Singapore Dollar", new() { Exact = true });

    private async Task OpenCombinationEdit(string name)
    {
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Variants", Exact = true }).ClickAsync();
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = name });
        await row.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
    }

    private async Task AssertCombinationFields(ILocator dialog, string name, string color, decimal price)
    {
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByLabel("Combination name", new() { Exact = true })).ToHaveValueAsync(name);
        await Expect(dialog.GetByLabel("Option value", new() { Exact = true }).Nth(0)).ToHaveValueAsync(color);
        await Expect(dialog.GetByLabel("Option value", new() { Exact = true }).Nth(1)).ToHaveValueAsync("Large");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Option type", Exact = true }).Nth(0)).ToContainTextAsync("Color (Tenant-wide)");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Option type", Exact = true }).Nth(1)).ToContainTextAsync("Size (Product-local)");
        decimal.Parse(await CombinationPrice(dialog).InputValueAsync(), System.Globalization.CultureInfo.InvariantCulture).Should().Be(price);
    }

    private void ConfigureCombinationWorkflow(Mock<IInventarioServiceWrapper> wrapper, Mock<IDataContext> data)
    {
        ReadRows<ProductVariation>(data, _state.BrowserCombinationVariations);
        wrapper.Setup(x => x.GetProductVariationTypes(It.Is<GetProductVariationTypesRequest>(_ => _state.CombinationWorkflow), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new QueryResponse<List<ProductVariationType>>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response =
                [
                    new() { Id = _state.CombinationColorId, TenantId = _state.ActiveTenant, Name = "Color", Code = "COLOR", IsEnabled = true },
                    new() { Id = _state.CombinationSizeId, TenantId = _state.ActiveTenant, ProductId = _state.PolishProductId, Name = "Size", Code = "SIZE", IsEnabled = true }
                ]
            });
        wrapper.Setup(x => x.CreateProductVariation(It.IsAny<CreateProductVariationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateProductVariationRequest request, CancellationToken _) => _state.CreateBrowserCombination(request));
        wrapper.Setup(x => x.UpdateProductVariation(It.IsAny<UpdateProductVariationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateProductVariationRequest request, CancellationToken _) => _state.UpdateBrowserCombination(request));
    }
}

public sealed partial class SetupBrowserState
{
    public bool CombinationWorkflow { get; set; }
    public Guid CombinationColorId { get; } = Guid.NewGuid();
    public Guid CombinationSizeId { get; } = Guid.NewGuid();
    public List<ProductVariation> CombinationVariations { get; } = [];
    public ConcurrentQueue<CreateProductVariationRequest> CombinationCreates { get; } = new();
    public ConcurrentQueue<UpdateProductVariationRequest> CombinationUpdates { get; } = new();

    private void ResetCombinationWorkflow()
    {
        CombinationWorkflow = false;
        CombinationVariations.Clear();
        CombinationCreates.Clear();
        CombinationUpdates.Clear();
    }

    public List<ProductVariation> BrowserCombinationVariations() => CombinationWorkflow ? CombinationVariations :
        VariationsOnly || PolishRuleScoped || ProductWorkflowHasVariant
            ? [new ProductVariation { Id = PolishVariantId, TenantId = ActiveTenant, ProductId = PolishProductId, Name = "Large", VariationType = "Size", Price = 20 }]
            : [];

    public CmdResponse CreateBrowserCombination(CreateProductVariationRequest request)
    {
        CombinationCreates.Enqueue(request with { CombinationOptions = request.CombinationOptions?.ToList() });
        CombinationVariations.Add(new ProductVariation
        {
            Id = Guid.NewGuid(), TenantId = request.Metadata.RequestedTenantId!.Value, ProductId = request.ProductId,
            Name = request.Name, Price = request.Price, VariationType = "Combination", IsEnabled = true,
            CombinationOptions = request.CombinationOptions?.ToList(),
            CombinationKey = ProductVariationCombination.CreateKey(request.CombinationOptions!), ConcurrencyStamp = Guid.NewGuid()
        });
        return new() { HttpStatusCode = HttpStatusCode.Created };
    }

    public CmdResponse UpdateBrowserCombination(UpdateProductVariationRequest request)
    {
        CombinationUpdates.Enqueue(request with { CombinationOptions = request.CombinationOptions?.ToList() });
        var variation = CombinationVariations.Single(x => x.Id == request.ProductVariationId);
        if (variation.ConcurrencyStamp != request.ExpectedConcurrencyStamp)
            return new() { HttpStatusCode = HttpStatusCode.Conflict };
        variation.Name = request.Name;
        variation.Price = request.Price;
        variation.CombinationOptions = request.CombinationOptions?.ToList();
        variation.CombinationKey = ProductVariationCombination.CreateKey(request.CombinationOptions!);
        variation.ConcurrencyStamp = Guid.NewGuid();
        return new() { HttpStatusCode = HttpStatusCode.OK };
    }
}
