using Community.Core.DataAccess.Commands.Entity.Content;
using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Api.SignalR.Handlers.Content;

public class DeleteContentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<DeleteContentRequest, DeleteContentCmd>(connection, mediator);
    }
}