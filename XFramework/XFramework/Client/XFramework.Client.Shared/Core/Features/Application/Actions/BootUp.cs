namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public record BootUp : BaseAction;
    
    protected class BootUpHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<BootUp>(handlerServices, store)
    {
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();
        
        public override async Task Handle(BootUp action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}