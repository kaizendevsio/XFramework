using XFramework.Blazor.Core.Features.Cart;

namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record ClearCart : StateAction;

    public class ClearCartHandler(HandlerServices handlerServices, IStore store) : StateActionHandler<ClearCart>(handlerServices, store)
    {
        public override async Task Handle(ClearCart action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            currentState.Products?.Clear();
            await Persist(currentState);
        }
    }
}