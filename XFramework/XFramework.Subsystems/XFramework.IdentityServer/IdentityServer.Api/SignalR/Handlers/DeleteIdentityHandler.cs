using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<DeleteIdentityRequest, DeleteIdentityCmd>(connection, mediator);
    }
}