namespace IdentityServer.Api.SignalR.Handlers;

public class CheckCredentialExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<CheckCredentialExistenceRequest, CheckCredentialExistenceQuery, ExistenceResponse>(connection, mediator);
    }
}