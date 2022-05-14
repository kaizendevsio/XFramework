using Community.Core.DataAccess.Commands.Entity.Connection;
using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Api.SignalR.Handlers.Connection;

public class CreateConnectionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<CreateConnectionRequest, CreateConnectionCmd>(connection, mediator);
    }
}