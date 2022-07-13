using IdentityServer.Core.DataAccess.Commands.Entity.Storage;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteFileHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteFileRequest, DeleteFileCmd>(connection, mediator);
    }
}