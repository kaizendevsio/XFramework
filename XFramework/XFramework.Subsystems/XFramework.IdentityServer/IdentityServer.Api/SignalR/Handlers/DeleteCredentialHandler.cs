using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<DeleteCredentialRequest, DeleteCredentialCmd>(connection, mediator);
    }
}