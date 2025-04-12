using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record RemoveProduct(CartProductViewModel Product) : StateAction;

    public class RemoveProductHandler : StateActionHandler<RemoveProduct>
    {
        private CartState CurrentState => Store.GetState<CartState>();

        public RemoveProductHandler(HandlerServices handlerServices, IStore store) : base(handlerServices, store) {}

        public override async Task Handle(RemoveProduct action, CancellationToken cancellationToken)
        {
            CurrentState.Products.Remove(action.Product);
            await Task.CompletedTask;
        }
    }
}