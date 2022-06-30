using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateCredentialRequest, CreateCredentialCmd>(connection, mediator);
    }
}