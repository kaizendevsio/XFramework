using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using FluentAssertions;
using XFramework.Portal.Features.Inventario;

namespace Portal.E2ETests;

[TestFixture]
public sealed class InventarioGridFilteringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FlatGrid_FiltersComputedProductLabel(bool queryable)
    {
        var labels = new Dictionary<string, string> { ["shirt"] = "E2E Shirt", ["water"] = "E2E Water" };
        var column = new InventoryTemplateColumn<Row>
        {
            Id = "product", Title = "Product", Filterable = true,
            SortBy = row => labels[row.ProductId], FilterBy = row => labels[row.ProductId]
        };
        var grid = new BlazorBlueprint.Components.BbDataGrid<Row>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BlazorBlueprint.Components.BbDataGrid<Row>);
        ((ICollection<IDataGridColumn<Row>>)type.GetField("_columns", flags)!.GetValue(grid)!).Add(column);
        var state = (DataGridState<Row>)type.GetField("_gridState", flags)!.GetValue(grid)!;
        state.Filtering.SetFilter("product", new FilterCondition { Operator = FilterOperator.Equals, Value = "E2E Shirt" });
        Row[] rows = [new("shirt", "Base product"), new("shirt", "Size: Medium"), new("water", "Base product")];
        var inputType = queryable ? typeof(IQueryable<Row>) : typeof(IEnumerable<Row>);
        var method = type.GetMethod("ApplyColumnFilters", flags, [inputType])!;
        var result = (IEnumerable<Row>)method.Invoke(grid, [queryable ? rows.AsQueryable() : rows])!;
        result.Should().Equal(rows.Take(2), "computed labels must use the projection, not a reflected Product property");
    }

    public sealed record Row(string ProductId, string Variant);
}
