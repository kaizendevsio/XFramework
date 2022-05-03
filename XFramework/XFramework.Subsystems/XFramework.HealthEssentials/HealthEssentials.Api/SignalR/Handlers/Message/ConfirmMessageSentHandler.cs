namespace HealthEssentials.Api.SignalR.Handlers.Message;

public class ConfirmMessageSentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<ConfirmMessageSentRequest, ConfirmMessageSentCmd>(connection, mediator);
    }
}