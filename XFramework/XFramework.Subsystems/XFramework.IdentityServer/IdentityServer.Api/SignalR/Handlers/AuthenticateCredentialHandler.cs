namespace IdentityServer.Api.SignalR.Handlers;

public class AuthenticateCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<AuthenticateCredentialRequest, AuthenticateCredentialQuery, AuthorizeIdentityResponse>(connection, mediator);
    }
}