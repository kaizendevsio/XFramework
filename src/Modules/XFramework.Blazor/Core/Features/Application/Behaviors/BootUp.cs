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
            if (CurrentState.StateRestored)
                return;

            await Mediator.Send(new RestoreStates(), aCancellationToken);
            await Mediator.Send(new SetState { StateRestored = true, IsBusy = false }, aCancellationToken);
            await Mediator.Publish(new StateRestoredEvent(), aCancellationToken);
        }
    }
}
