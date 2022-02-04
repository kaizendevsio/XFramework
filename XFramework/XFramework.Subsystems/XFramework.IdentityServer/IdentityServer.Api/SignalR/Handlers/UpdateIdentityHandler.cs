using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Api.SignalR.Handlers;

public class UpdateIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateIdentityRequest, UpdateIdentityCmd>(connection, mediator);
    }
}