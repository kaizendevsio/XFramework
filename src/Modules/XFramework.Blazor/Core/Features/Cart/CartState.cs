using XFramework.Blazor.Core.Features.Cart.Models;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState : State<CartState>
{
    public override void Initialize()
    {
        Items = [];
    }

    public List<CartItemVm>? Items { get; set; } = [];
    public decimal Total => Items?.Sum(p => p.Total) ?? 0;
    public int TotalItems => Items?.Sum(p => p.Quantity) ?? 0;
}