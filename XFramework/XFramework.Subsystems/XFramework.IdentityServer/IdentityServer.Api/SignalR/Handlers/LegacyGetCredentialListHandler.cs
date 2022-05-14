namespace IdentityServer.Api.SignalR.Handlers;

public class LegacyGetCredentialListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetCredentialListRequest, GetCredentialListQuery, List<CredentialResponse>>(connection, mediator);
    }
}