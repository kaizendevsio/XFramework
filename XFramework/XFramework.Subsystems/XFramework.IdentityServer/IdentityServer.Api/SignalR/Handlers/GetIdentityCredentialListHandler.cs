namespace IdentityServer.Api.SignalR.Handlers;

public class GetIdentityCredentialListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetIdentityCredentialListRequest, GetIdentityCredentialListQuery>(connection, mediator);
    }
}