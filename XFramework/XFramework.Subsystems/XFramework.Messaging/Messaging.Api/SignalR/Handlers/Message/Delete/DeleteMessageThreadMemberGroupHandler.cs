using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Delete;

namespace Messaging.Api.SignalR.Handlers.Message.Delete;

public class DeleteMessageThreadMemberGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMessageThreadMemberGroupRequest, DeleteMessageThreadMemberGroupCmd>(connection, mediator);
    }
}