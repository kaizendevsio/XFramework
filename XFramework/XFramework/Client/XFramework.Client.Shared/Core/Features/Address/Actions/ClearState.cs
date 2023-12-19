namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public record ClearState : SetState;

    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<ClearState>(handlerServices, store)
    {
        private AddressState CurrentState => Store.GetState<AddressState>();

        public override async Task Handle(ClearState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.ClearProperties(action, CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
}