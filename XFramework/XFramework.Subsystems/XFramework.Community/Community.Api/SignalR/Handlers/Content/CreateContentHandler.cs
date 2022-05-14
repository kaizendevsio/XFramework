using Community.Core.DataAccess.Commands.Entity.Content;
using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Api.SignalR.Handlers.Content;

public class CreateContentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<CreateContentRequest, CreateContentCmd>(connection, mediator);
    }
}