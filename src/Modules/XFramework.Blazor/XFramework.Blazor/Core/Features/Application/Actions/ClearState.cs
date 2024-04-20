namespace XFramework.Blazor.Core.Features.Application;

public partial class ApplicationState
{
    public record ClearState : SetState;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<ClearState>(handlerServices, store)
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