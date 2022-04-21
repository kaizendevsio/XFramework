using Community.Core.DataAccess.Commands.Entity.Reaction;
using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Api.SignalR.Handlers.Reaction;

public class DeleteReactionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<DeleteReactionRequest, DeleteReactionCmd>(connection, mediator);
    }
}