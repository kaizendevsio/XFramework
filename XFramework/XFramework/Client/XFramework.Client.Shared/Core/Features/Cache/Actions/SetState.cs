namespace XFramework.Client.Shared.Core.Features.Cache;

public partial class CacheState
{
    public class SetState : BaseAction;
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<SetState>(handlerServices, store)
    {
        private CacheState CurrentState => Store.GetState<CacheState>();
        
        public override async Task Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action, CurrentState);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
}