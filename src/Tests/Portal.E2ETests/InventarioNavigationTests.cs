using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Portal.Components.Layout;
using XFramework.Portal.Features.Inventario.Pages;

namespace Portal.E2ETests;

[TestFixture]
public sealed class InventarioNavigationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("summary")]
    [TestCase("stock")]
    [TestCase("lots")]
    public void ProductSidebar_SectionLinksRetainCategoryScope(string section)
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var sidebar = new ProductDetailSidebar { ProductId = productId };
        var navigation = new TestNavigation($"/inventario/products/{productId}?categoryId={categoryId}");
        typeof(ProductDetailSidebar).GetProperty("Navigation", PrivateInstance)!.SetValue(sidebar, navigation);

        var href = typeof(ProductDetailSidebar).GetMethod("SectionHref", PrivateInstance)!.Invoke(sidebar, [section]);

        href.Should().Be($"/inventario/products/{productId}{(section == "summary" ? "" : $"/{section}")}?categoryId={categoryId}");
    }

    [Test]
    public void ProductCategoryScope_ClearingQueryRestoresOtherProducts()
    {
        var categoryId = Guid.NewGuid();
        var scoped = new Product { Id = Guid.NewGuid(), CategoryId = categoryId, Name = "Shirt" };
        var other = new Product { Id = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Water" };
        var page = new Products { CategoryId = categoryId };
        typeof(Products).GetField("_products", PrivateInstance)!.SetValue(page, new List<Product> { scoped, other });
        var update = typeof(Products).GetMethod("OnParametersSet", PrivateInstance)!;

        update.Invoke(page, null);
        ((List<Product>)typeof(Products).GetField("_filteredProducts", PrivateInstance)!.GetValue(page)!).Should().Equal(scoped);

        page.CategoryId = null;
        update.Invoke(page, null);
        ((List<Product>)typeof(Products).GetField("_filteredProducts", PrivateInstance)!.GetValue(page)!).Should().Equal(scoped, other);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PurchaseOrderBack_RetainsOnlySuppliedSupplierScope(bool scoped)
    {
        var supplierId = scoped ? Guid.NewGuid() : (Guid?)null;
        var page = new PurchaseOrderDetail { SupplierId = supplierId };
        var navigation = new TestNavigation("/inventario/purchase-orders/00000000-0000-0000-0000-000000000001");
        typeof(PurchaseOrderDetail).GetProperty("Navigation", PrivateInstance)!.SetValue(page, navigation);

        typeof(PurchaseOrderDetail).GetMethod("GoBack", PrivateInstance)!.Invoke(page, null);

        navigation.Uri.Should().Be("https://portal.test/inventario/purchase-orders" + (supplierId is { } id ? $"?supplierId={id}" : ""));
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(-1, false)]
    public void ProductMovement_RequiresNonzeroQuantityAndSelectedLocation(int quantity, bool disabled)
    {
        var product = new Product { Id = Guid.NewGuid() };
        var warehouse = new Warehouse { Id = Guid.NewGuid() };
        var location = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id };
        var variantId = Guid.NewGuid();
        var page = new ProductDetail();
        var type = typeof(ProductDetail);
        type.GetField("_product", PrivateInstance)!.SetValue(page, product);
        type.GetField("_warehouses", PrivateInstance)!.SetValue(page, new List<Warehouse> { warehouse });
        type.GetField("_locations", PrivateInstance)!.SetValue(page, new List<InventoryLocation> { location });
        type.GetField("_balances", PrivateInstance)!.SetValue(page, new List<StockBalance>
        {
            new() { ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, AvailableQuantity = 8 },
            new() { ProductId = product.Id, ProductVariationId = variantId, WarehouseId = warehouse.Id, LocationId = location.Id, AvailableQuantity = 7 }
        });
        var form = type.GetField("_movementForm", PrivateInstance)!.GetValue(page)!;
        void Set(string name, object value) => form.GetType().GetProperty(name)!.SetValue(form, value);
        Set("WarehouseId", warehouse.Id.ToString());
        Set("LocationId", location.Id.ToString());
        Set("Quantity", (decimal)quantity);

        type.GetProperty("IsPostMovementDisabled", PrivateInstance)!.GetValue(page).Should().Be(disabled);
        type.GetProperty("SelectedMovementAvailable", PrivateInstance)!.GetValue(page).Should().Be(8m);
        Set("ProductVariationId", variantId.ToString());
        type.GetProperty("SelectedMovementAvailable", PrivateInstance)!.GetValue(page).Should().Be(7m);
        Set("LocationId", "");
        type.GetProperty("IsPostMovementDisabled", PrivateInstance)!.GetValue(page).Should().Be(true);
        type.GetProperty("SelectedMovementAvailable", PrivateInstance)!.GetValue(page).Should().Be(0m);
    }

    [Test]
    public void ReservationQuantity_OverAvailableStockIsActionableAndDisabled()
    {
        var page = new Reservations();
        var type = typeof(Reservations);
        var product = new Product { Id = Guid.NewGuid() };
        var warehouse = new Warehouse { Id = Guid.NewGuid() };
        var location = new InventoryLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id };
        type.GetField("_products", PrivateInstance)!.SetValue(page, new List<Product> { product });
        type.GetField("_warehouses", PrivateInstance)!.SetValue(page, new List<Warehouse> { warehouse });
        type.GetField("_locations", PrivateInstance)!.SetValue(page, new List<InventoryLocation> { location });
        var expiredLot = new InventoryLot { Id = Guid.NewGuid(), Status = XFramework.Inventario.Domain.Shared.Enums.InventoryLotStatus.Available, ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        type.GetField("_lots", PrivateInstance)!.SetValue(page, new List<InventoryLot> { expiredLot });
        type.GetField("_balances", PrivateInstance)!.SetValue(page, new List<StockBalance>
        {
            new() { ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, AvailableQuantity = 10 },
            new() { ProductId = product.Id, ProductVariationId = Guid.NewGuid(), WarehouseId = warehouse.Id, LocationId = location.Id, AvailableQuantity = 100 },
            new() { ProductId = product.Id, WarehouseId = warehouse.Id, LocationId = location.Id, LotId = expiredLot.Id, AvailableQuantity = 7 }
        });
        var form = type.GetField("_form", PrivateInstance)!.GetValue(page)!;
        void Set(string name, object value) => form.GetType().GetProperty(name)!.SetValue(form, value);
        Set("ProductId", product.Id.ToString());
        Set("WarehouseId", warehouse.Id.ToString());
        Set("LocationId", location.Id.ToString());
        Set("Quantity", 11m);

        var message = type.GetProperty("QuantityValidationMessage", PrivateInstance)!;
        message.GetValue(page).Should().Be("Requested quantity 11 exceeds the 10 currently available. Reduce the quantity or select another stock position.");
        type.GetProperty("IsReserveDisabled", PrivateInstance)!.GetValue(page).Should().Be(true);

        Set("Quantity", 10m);
        message.GetValue(page).Should().BeNull();
        type.GetProperty("IsReserveDisabled", PrivateInstance)!.GetValue(page).Should().Be(false);

        Set("AllowExpiredLotOverride", true);
        type.GetProperty("AvailableStock", PrivateInstance)!.GetValue(page).Should().Be(17m, "the existing explicit expiry override includes the expired lot but never other variants");
    }

    [Test]
    public void ReservationFailure_KnownStockConflictIsActionableAndUnknownDetailsStayPrivate()
    {
        var method = typeof(Reservations).GetMethod("ReservationFailureMessage", BindingFlags.Static | BindingFlags.NonPublic)!;
        var response = new XFramework.Domain.Shared.BusinessObjects.CmdResponse
        {
            HttpStatusCode = System.Net.HttpStatusCode.Conflict,
            Message = "Insufficient allocatable stock for the requested reservation quantity."
        };
        ((string)method.Invoke(null, [response])!).Should().Contain("Not enough eligible stock").And.Contain("Reduce the quantity");

        response.HttpStatusCode = System.Net.HttpStatusCode.InternalServerError;
        response.Message = "Database provider and connection details";
        ((string)method.Invoke(null, [response])!).Should().Contain("draft is retained").And.NotContain(response.Message);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string path) => Initialize("https://portal.test/", "https://portal.test" + path);
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }
}
