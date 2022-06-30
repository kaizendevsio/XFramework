using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateIdentityRequest, CreateIdentityCmd>(connection, mediator);
    }
}