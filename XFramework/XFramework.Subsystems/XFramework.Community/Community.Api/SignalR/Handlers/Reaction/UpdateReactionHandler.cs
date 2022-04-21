using Community.Core.DataAccess.Commands.Entity.Reaction;
using Community.Domain.Generic.Contracts.Requests.Update;

namespace Community.Api.SignalR.Handlers.Reaction;

public class UpdateReactionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateReactionRequest, UpdateReactionCmd>(connection, mediator);
    }
}