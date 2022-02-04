using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<DeleteCredentialRequest, DeleteCredentialCmd>(connection, mediator);
    }
}