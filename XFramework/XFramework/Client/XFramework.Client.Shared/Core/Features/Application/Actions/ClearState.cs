namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class ClearState : SetState;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<ClearState>(handlerServices, store)
    {
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();

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