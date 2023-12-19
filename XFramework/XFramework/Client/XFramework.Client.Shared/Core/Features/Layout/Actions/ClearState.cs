namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    public record ClearState : SetState;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<ClearState>(handlerServices, store)
    {
        private LayoutState CurrentState => Store.GetState<LayoutState>();
        

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