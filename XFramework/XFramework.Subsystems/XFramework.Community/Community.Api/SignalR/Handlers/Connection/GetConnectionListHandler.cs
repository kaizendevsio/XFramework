using Community.Core.DataAccess.Commands.Entity.Connection;
using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Api.SignalR.Handlers.Connection;

public class GetConnectionListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<DeleteConnectionRequest, DeleteConnectionCmd>(connection, mediator);
    }
}