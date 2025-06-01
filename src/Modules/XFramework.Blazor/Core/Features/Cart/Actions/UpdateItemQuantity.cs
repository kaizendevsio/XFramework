using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record UpdateItemQuantity(CartItemVm Item, int Quantity) : StateAction;

    public class UpdateItemQuantityHandler(HandlerServices handlerServices, IStore store) : StateActionHandler<UpdateItemQuantity>(handlerServices, store)
    {
        public override async Task Handle(UpdateItemQuantity action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            // Find the item in the current state list using the ID from the action's item
            var itemInState = currentState.Items?.FirstOrDefault(i => i.Id == action.Item.Id); 
            if (itemInState != null)
            {
                itemInState.Quantity = action.Quantity;
                await Persist(currentState);
            }
        }
    }
}
