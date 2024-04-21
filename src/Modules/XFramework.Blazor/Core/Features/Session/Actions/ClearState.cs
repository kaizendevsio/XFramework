namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record ClearState : SetState;
    
    protected class ClearStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<ClearState>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(ClearState state, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.ClearProperties(state, CurrentState);
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