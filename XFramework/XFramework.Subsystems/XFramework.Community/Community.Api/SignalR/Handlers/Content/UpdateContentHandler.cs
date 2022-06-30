using Community.Core.DataAccess.Commands.Entity.Content;
using Community.Domain.Generic.Contracts.Requests.Update;

namespace Community.Api.SignalR.Handlers.Content;

public class UpdateContentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<UpdateContentRequest, UpdateContentCmd>(connection, mediator);
    }
}