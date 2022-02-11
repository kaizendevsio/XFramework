namespace IdentityServer.Api.SignalR.Handlers;

public class GetCredentialListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetCredentialListRequest, GetCredentialListQuery>(connection, mediator);
    }
}