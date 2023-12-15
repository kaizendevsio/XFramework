using XFramework.Client.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class StateRestoredEvent : IEvent;
    
    protected class StateRestoredEventHandler(IMessageBusWrapper messageBusWrapper,HandlerServices handlerServices, IStore store)
        : EventHandler<StateRestoredEvent>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(StateRestoredEvent action, CancellationToken cancellationToken)
        {
            await messageBusWrapper.StartClientEventListener($"{CurrentState.Credential.Id}");
        }
    }
}