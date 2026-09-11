using System.Linq.Expressions;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.DataGrid;

namespace XFramework.Portal.Features.Inventario;

// BlazorBlueprint 3.16 template columns omit this projection from their interface.
// Flat grids otherwise reflect a property name and cannot filter lookup labels.
public sealed class InventoryTemplateColumn<TData> : BbDataGridTemplateColumn<TData>, IDataGridColumn<TData>
    where TData : class
{
    LambdaExpression? IDataGridColumn<TData>.GetSortAndFilterExpression() => GetFilterExpression();
}
