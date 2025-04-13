using XFramework.Blazor.Core.Features.Cart;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record Checkout(EventCallback Callback) : StateAction;

    public class CheckoutHandler(HandlerServices handlerServices, IStore store) : StateActionHandler<Checkout>(handlerServices, store)
    {
        public override async Task Handle(Checkout action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            if (action.Callback.HasDelegate)
            {
                await action.Callback.InvokeAsync(null);
            }
            currentState.Products?.Clear();
            await Persist(currentState);
        }
    }
}