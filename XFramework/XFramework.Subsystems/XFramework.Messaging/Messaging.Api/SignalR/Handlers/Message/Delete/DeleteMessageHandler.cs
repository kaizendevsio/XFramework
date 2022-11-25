using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Delete;

namespace Messaging.Api.SignalR.Handlers.Message.Delete;

public class DeleteMessageHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMessageRequest, DeleteMessageCmd>(connection, mediator);
    }
}