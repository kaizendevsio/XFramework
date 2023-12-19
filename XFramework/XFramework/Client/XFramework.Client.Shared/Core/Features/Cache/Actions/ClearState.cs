namespace XFramework.Client.Shared.Core.Features.Cache;

public partial class CacheState
{
    public record ClearState : BaseAction;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<ClearState>(handlerServices, store)
    {
        private CacheState CurrentState => Store.GetState<CacheState>();

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