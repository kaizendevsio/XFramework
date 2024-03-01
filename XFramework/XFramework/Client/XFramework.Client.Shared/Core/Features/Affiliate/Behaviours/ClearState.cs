namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AffiliateState
{
    public record ClearState : SetState;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<ClearState>(handlerServices, store)
    {
        private AffiliateState CurrentState => Store.GetState<AffiliateState>();

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