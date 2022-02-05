using IdentityServer.Domain.Generic.Contracts.Requests.Check;

namespace IdentityServer.Api.SignalR.Handlers;

public class AuthenticateHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<AuthenticateCredentialRequest, AuthenticateCredentialQuery>(connection, mediator);
    }
}