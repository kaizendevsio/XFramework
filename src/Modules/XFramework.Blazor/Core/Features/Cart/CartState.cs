using XFramework.Blazor.Core.Features.Cart.Models;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState : State<CartState>
{
    public override void Initialize()
    {
        Products = new List<CartProductViewModel>();
    }

    public List<CartProductViewModel>? Products { get; set; } = new();
}