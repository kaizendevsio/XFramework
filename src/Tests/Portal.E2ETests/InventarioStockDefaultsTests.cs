using System.Net;
using System.Reflection;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.JSInterop;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Unit"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed class InventarioStockDefaultsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();
    private readonly Warehouse _first = new() { Id = Guid.NewGuid(), IsEnabled = true };
    private readonly Warehouse _preferred = new() { Id = Guid.NewGuid(), IsEnabled = true };

    [Test]
    public async Task Movement_DefaultStockArea_PreservesProductScopeAndExplicitBalance()
    {
        var location = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = _preferred.Id, IsEnabled = true };
        var wrapper = Wrapper(location);
        var page = new ProductDetail { Id = _product };
        Configure(page, wrapper.Object, location);
        await Invoke(page, "OpenMovementDialog");
        AssertSelection(page, "_movementForm", _preferred.Id, location.Id);
        Value(page, "_movementForm", "ProductId").Should().Be(_product.ToString());

        var balance = new StockBalance { ProductId = _product, WarehouseId = _first.Id, LocationId = Guid.NewGuid(), ProductVariationId = Guid.NewGuid(), LotId = Guid.NewGuid() };
        await Invoke(page, "OpenForBalance", balance);
        AssertSelection(page, "_movementForm", balance.WarehouseId, balance.LocationId);
        Value(page, "_movementForm", "ProductVariationId").Should().Be(balance.ProductVariationId.ToString());
        Value(page, "_movementForm", "LotId").Should().Be(balance.LotId.ToString());
        wrapper.Verify(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Receiving_NewDialog_PrefillsSavedStockArea()
    {
        var location = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = _preferred.Id, IsEnabled = true };
        var wrapper = Wrapper(location);
        var page = new Receiving();
        Configure(page, wrapper.Object, location);
        await Invoke(page, "OpenReceiveDialog");
        AssertSelection(page, "_form", _preferred.Id, location.Id);
        wrapper.Verify(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Movement_UnrelatedSavedLocation_UsesLocationWithinSelectedWarehouse()
    {
        var unrelated = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = _first.Id, IsEnabled = true };
        var fallback = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = _preferred.Id, IsEnabled = true };
        var wrapper = Wrapper(unrelated);
        var page = new ProductDetail { Id = _product };
        Configure(page, wrapper.Object, unrelated);
        Set(page, "_locations", new List<InventoryLocation> { unrelated, fallback });
        await Invoke(page, "OpenMovementDialog");
        AssertSelection(page, "_movementForm", _preferred.Id, fallback.Id);
    }

    private Mock<IInventarioServiceWrapper> Wrapper(InventoryLocation location)
    {
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.GetInventarioSetup(It.Is<GetInventarioSetupRequest>(r => r.Metadata.RequestedTenantId == _tenant), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<InventarioSetupResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new() { TenantId = _tenant, WarehouseId = _preferred.Id, LocationId = location.Id }
            });
        return wrapper;
    }

    private void Configure(object page, IInventarioServiceWrapper wrapper, InventoryLocation location)
    {
        var context = new Mock<IPortalTenantContext>();
        context.SetupGet(x => x.SelectedTenantId).Returns(_tenant);
        page.GetType().GetProperty("TenantFilter", Private)!.SetValue(page, context.Object);
        page.GetType().GetProperty("Inventario", Private)!.SetValue(page, wrapper);
        page.GetType().GetProperty("FocusJs", Private)?.SetValue(page, new FocusJavaScript());
        Set(page, "_warehouses", new List<Warehouse> { _first, _preferred });
        Set(page, "_locations", new List<InventoryLocation> { location });
    }

    private static void AssertSelection(object page, string form, Guid warehouse, Guid location)
    {
        Value(page, form, "WarehouseId").Should().Be(warehouse.ToString());
        Value(page, form, "LocationId").Should().Be(location.ToString());
    }
    private static string Value(object page, string form, string property)
    {
        var model = page.GetType().GetField(form, Private)!.GetValue(page)!;
        return (string)model.GetType().GetProperty(property)!.GetValue(model)!;
    }
    private static void Set(object page, string field, object value) => page.GetType().GetField(field, Private)!.SetValue(page, value);
    private static Task Invoke(object page, string method, params object[] args) => (Task)page.GetType().GetMethod(method, Private)!.Invoke(page, args)!;
    private sealed class FocusJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
