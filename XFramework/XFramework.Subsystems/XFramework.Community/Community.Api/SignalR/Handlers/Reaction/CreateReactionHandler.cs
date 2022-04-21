using Community.Core.DataAccess.Commands.Entity.Reaction;
using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Api.SignalR.Handlers.Reaction;

public class CreateReactionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateReactionRequest, CreateReactionCmd>(connection, mediator);
    }
}