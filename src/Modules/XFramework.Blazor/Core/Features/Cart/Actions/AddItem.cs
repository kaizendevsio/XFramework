using XFramework.Blazor.Core.Features.Cart.Models;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record AddItem(IProduct Product) : StateAction;

    public class AddItemHandler : StateActionHandler<AddItem>
    {
        public AddItemHandler(HandlerServices handlerServices, IStore store) : base(handlerServices, store) {}

        public override async Task Handle(AddItem action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            currentState.Items ??= []; // Ensure the list is initialized

            var existingItem = currentState.Items.FirstOrDefault(i => i.Product.Id == action.Product.Id);

            if (existingItem != null)
            {
                existingItem.Quantity++; // Increment quantity if item exists
            }
            else
            {
                var newItem = new CartItemVm // Create and add new item if it doesn't exist
                {
                    Product = action.Product,
                    Quantity = 1 
                };
                currentState.Items.Add(newItem);
            }
            
            await Persist(currentState);
        }
    }
}
