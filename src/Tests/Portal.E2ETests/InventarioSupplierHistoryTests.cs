using System.Linq.Expressions;
using System.Reflection;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using FluentAssertions;
using Moq;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.ExpressionVisitor;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Features.Inventario.Components;

namespace Portal.E2ETests;

[TestFixture]
public sealed class InventarioSupplierHistoryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public async Task Orders_PageBeyond500_IsTenantSupplierScopedAndDoesNotReadOtherTabs()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<PurchaseOrder>(data);
        using var component = Component(data);
        var result = await Invoke<PurchaseOrder>(component, "LoadOrders", new() { StartIndex = 510, Count = 10 });
        result.TotalItemCount.Should().Be(523);
        query.Verify(x => x.CountAsync(It.IsAny<CancellationToken>()), Times.Once);
        query.Verify(x => x.Skip(510), Times.Once);
        query.Verify(x => x.Take(10), Times.Once);
        var matches = Predicate(query);
        matches(new() { TenantId = component.TenantId, SupplierId = component.SupplierId }).Should().BeTrue();
        matches(new() { TenantId = Guid.NewGuid(), SupplierId = component.SupplierId }).Should().BeFalse();
        matches(new() { TenantId = component.TenantId, SupplierId = Guid.NewGuid() }).Should().BeFalse();
        matches(new() { TenantId = component.TenantId, SupplierId = component.SupplierId, IsDeleted = true }).Should().BeFalse();
        data.Verify(x => x.Query<ReceivingDocument>(), Times.Never);
        data.Verify(x => x.Query<ReceivingLine>(), Times.Never);
    }

    [Test]
    public async Task DisabledDependencies_DoNotIssueQueries()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        using var component = Component(data);
        component.MovementsEnabled = false;
        await Invoke<ReceivingLine>(component, "LoadMovements", new());
        component.PurchasingEnabled = false;
        await Invoke<PurchaseOrder>(component, "LoadOrders", new());
        await Invoke<ReceivingDocument>(component, "LoadReceipts", new());
        component.MovementsEnabled = true;
        await Invoke<ReceivingLine>(component, "LoadMovements", new());
        data.Invocations.Should().BeEmpty();
    }

    [Test]
    public async Task UnsupportedFilter_ReportsOnlyItsTabErrorWithoutQueryingUnfilteredRows()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<PurchaseOrder>(data);
        using var component = Component(data);
        await Invoke<PurchaseOrder>(component, "LoadOrders", new()
        {
            Filters = new Dictionary<string, FilterCondition> { ["OrderNumber"] = new() { Operator = FilterOperator.NotContains, Value = "PO" } }
        });
        typeof(SupplierHistory).GetField("_ordersError", Private)!.GetValue(component).Should().BeOfType<string>().Which.Should().Contain("filter is not supported");
        typeof(SupplierHistory).GetField("_receiptsError", Private)!.GetValue(component).Should().BeNull();
        query.Verify(x => x.ToListAsync(It.IsAny<CancellationToken>()), Times.Never);
        query.Verify(x => x.CountAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MovementRows_RemainVisibleWhenCatalogDeclinesAccess()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<ReceivingLine>(data);
        var row = new ReceivingLine { ProductId = Guid.NewGuid(), InventoryMovementId = Guid.NewGuid() };
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([row]);
        data.Setup(x => x.Query<Product>()).Throws(new UnauthorizedAccessException("Catalog declined"));
        using var component = Component(data);
        var result = await Invoke<ReceivingLine>(component, "LoadMovements", new() { Count = 10 });
        result.Items.Should().ContainSingle().Which.Should().BeSameAs(row);
        var matches = Predicate(query);
        row.TenantId = component.TenantId;
        row.ReceivingDocument = new() { TenantId = component.TenantId, SupplierId = component.SupplierId };
        matches(row).Should().BeTrue();
        row.ReceivingDocument.SupplierId = Guid.NewGuid(); matches(row).Should().BeFalse();
        component.ProductsEnabled = false;
        await Invoke<ReceivingLine>(component, "LoadMovements", new() { Count = 10 });
        data.Verify(x => x.Query<Product>(), Times.Once);
    }

    [TestCase(FilterOperator.Contains)]
    [TestCase(FilterOperator.Equals)]
    [TestCase(FilterOperator.IsEmpty)]
    [TestCase(FilterOperator.IsNotEmpty)]
    public void NativeTextFilters_AreTransportable(FilterOperator operation)
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<PurchaseOrder>(data);
        SupplierHistoryQuery.Apply(query.Object, new DataGridRequest
        {
            Filters = new Dictionary<string, FilterCondition> { ["ordernumber"] = new() { Field = "OrderNumber", Operator = operation, Value = "PO" } }
        }, "OrderDate", "OrderDate", "OrderNumber");
        Predicate(query);
    }

    [Test]
    public void NativeDateAndStatusFilters_AreTransportable()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<PurchaseOrder>(data);
        SupplierHistoryQuery.Apply(query.Object, new DataGridRequest
        {
            Filters = new Dictionary<string, FilterCondition>
            {
                ["OrderDate"] = new() { Field = "OrderDate", Operator = FilterOperator.GreaterOrEqual, Value = DateTime.UtcNow.Date },
                ["Status"] = new() { Field = "Status", Operator = FilterOperator.Equals, Value = PurchaseOrderStatus.Draft.ToString() }
            }
        }, "OrderDate", "OrderDate", "Status");
        foreach (var invocation in query.Invocations.Where(x => x.Method.Name == "Where"))
            QueryExpressionVisitor.Parse((Expression<Func<PurchaseOrder, bool>>)invocation.Arguments[0]).Should().NotBeEmpty();
    }

    [Test]
    public void ProductNameFilter_IsTransportableAndUnknownFieldsAreRejected()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var query = Query<ReceivingLine>(data);
        var request = new DataGridRequest
        {
            Filters = new Dictionary<string, FilterCondition> { ["Product.Name"] = new() { Field = "Product.Name", Operator = FilterOperator.Contains, Value = "Water" } }
        };
        SupplierHistoryQuery.Apply(query.Object, request, "CreatedAt", "Product.Name", "CreatedAt");
        var matches = Predicate(query);
        matches(new() { Product = new() { Name = "Water bottle" } }).Should().BeTrue();
        var unsupported = () => SupplierHistoryQuery.Apply(query.Object, request, "CreatedAt", "CreatedAt");
        unsupported.Should().Throw<NotSupportedException>();
    }

    private static SupplierHistory Component(Mock<IDataContext> data)
    {
        var component = new SupplierHistory { TenantId = Guid.NewGuid(), SupplierId = Guid.NewGuid(), PurchasingEnabled = true, MovementsEnabled = true, ProductsEnabled = true };
        typeof(SupplierHistory).GetProperty("DataContext", Private)!.SetValue(component, data.Object);
        return component;
    }

    private static ValueTask<DataGridResult<T>> Invoke<T>(SupplierHistory component, string method, DataGridRequest request) =>
        (ValueTask<DataGridResult<T>>)typeof(SupplierHistory).GetMethod(method, Private)!.Invoke(component, [request])!;

    private static Func<T, bool> Predicate<T>(Mock<IRemoteQuery<T>> query) where T : class
    {
        var expression = (Expression<Func<T, bool>>)query.Invocations.First(x => x.Method.Name == "Where").Arguments[0];
        QueryExpressionVisitor.Parse(expression).Should().NotBeEmpty();
        return expression.Compile();
    }

    private static Mock<IRemoteQuery<T>> Query<T>(Mock<IDataContext> data) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, object>>>())).Returns(query.Object);
        query.Setup(x => x.ThenBy(It.IsAny<Expression<Func<T, Guid>>>())).Returns(query.Object);
        query.Setup(x => x.Skip(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(523);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        data.Setup(x => x.Query<T>()).Returns(query.Object);
        return query;
    }
}
