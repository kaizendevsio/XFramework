using Community.Core.DataAccess.Commands.Entity.Connection;
using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Api.SignalR.Handlers.Connection;

public class DeleteConnectionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<DeleteConnectionRequest, DeleteConnectionCmd>(connection, mediator);
    }
}