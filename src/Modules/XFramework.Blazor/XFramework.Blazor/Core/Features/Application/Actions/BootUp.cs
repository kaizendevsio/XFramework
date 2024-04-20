namespace XFramework.Blazor.Core.Features.Application;

public partial class ApplicationState
{
    public record BootUp : StateAction;
    
    protected class BootUpHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<BootUp>(handlerServices, store)
    {
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();
        
        public override async Task Handle(BootUp action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}