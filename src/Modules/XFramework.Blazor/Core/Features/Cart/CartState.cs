using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState : State<CartState>
{
    public override void Initialize()
    {
        Products = new List<Product>();
    }

    public List<Product> Products { get; set; }
}