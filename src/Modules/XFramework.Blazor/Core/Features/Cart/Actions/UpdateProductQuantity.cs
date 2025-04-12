using XFramework.Blazor.Core.Features.Cart;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record UpdateProductQuantity(Product Product, int Quantity) : StateAction;

    public class UpdateProductQuantityHandler(HandlerServices handlerServices, IStore store) : StateActionHandler<UpdateProductQuantity>(handlerServices, store)
    {
        public override async Task Handle(UpdateProductQuantity action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            var product = currentState.Products?.FirstOrDefault(p => p.Product?.Id == action.Product.Id);
            if (product != null)
            {
                product.Quantity = action.Quantity;
                await Persist(currentState);
            }
        }
    }
}