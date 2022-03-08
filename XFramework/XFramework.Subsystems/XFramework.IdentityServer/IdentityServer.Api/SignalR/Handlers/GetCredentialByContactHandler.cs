namespace IdentityServer.Api.SignalR.Handlers;

public class GetCredentialByContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetCredentialByContactRequest, GetCredentialByContactQuery>(connection, mediator);
    }
}