using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record AddProduct(Product Product) : StateAction;

    public class AddProductHandler : StateActionHandler<AddProduct>
    {
        public AddProductHandler(HandlerServices handlerServices, IStore store) : base(handlerServices, store) {}

        public override async Task Handle(AddProduct action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            var productViewModel = new CartProductViewModel
            {
                Product = action.Product,
                Quantity = 1 // Default quantity
            };
            currentState.Products?.Add(productViewModel);
            await Persist(currentState);
        }
    }
}