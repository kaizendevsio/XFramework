namespace XFramework.Blazor.Core.Features.Cart;

public partial class CartState
{
    public record Clear : StateAction;

    public class ClearHandler(HandlerServices handlerServices, IStore store) : StateActionHandler<Clear>(handlerServices, store)
    {
        public override async Task Handle(Clear action, CancellationToken aCancellationToken)
        {
            var currentState = Store.GetState<CartState>();
            currentState.Items?.Clear();
            await Persist(currentState);
        }
    }
}