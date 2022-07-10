using IdentityServer.Core.DataAccess.Commands.Entity.Storage;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateFileHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateFileRequest, CreateFileCmd>(connection, mediator);
    }
}