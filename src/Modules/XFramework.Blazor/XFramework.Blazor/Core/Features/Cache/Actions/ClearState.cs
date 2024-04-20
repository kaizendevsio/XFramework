namespace XFramework.Blazor.Core.Features.Cache;

public partial class CacheState
{
    public record ClearState : StateAction;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<ClearState>(handlerServices, store)
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