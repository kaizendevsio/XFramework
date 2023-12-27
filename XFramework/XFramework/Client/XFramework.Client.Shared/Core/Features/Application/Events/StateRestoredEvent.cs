using XFramework.Client.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class StateRestoredEvent : IEvent;
    
    protected class StateRestoredEventHandler(IMessageBusWrapper messageBusWrapper,HandlerServices handlerServices, IStore store)
        : EventHandler<StateRestoredEvent>(handlerServices, store)
    {
        public override async Task Handle(StateRestoredEvent action, CancellationToken cancellationToken)
        {
            if (SessionState.State is CurrentSessionState.Active)
            {
                await messageBusWrapper.StartClientEventListener($"{SessionState.Credential.Id}");
            }
        }
    }
}