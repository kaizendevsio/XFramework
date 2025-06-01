using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record RemoveItem(CartItemVm CartItem) : StateAction;

    public class RemoveItemHandler : StateActionHandler<RemoveItem>
    {
        public RemoveItemHandler(HandlerServices handlerServices, IStore store) : base(handlerServices, store) {}

        public override async Task Handle(RemoveItem action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            currentState.Items?.RemoveAll(p => p.Id == action.CartItem.Id);
            await Persist(currentState);
        }
    }
}