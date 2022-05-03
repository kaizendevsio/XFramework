namespace HealthEssentials.Api.SignalR.Handlers.Message;

public class CreateDirectMessageHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateDirectMessageRequest, CreateDirectMessageCmd>(connection, mediator);
    }
}