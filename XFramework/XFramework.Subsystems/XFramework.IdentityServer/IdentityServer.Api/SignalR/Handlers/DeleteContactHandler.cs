using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<DeleteContactRequest, DeleteContactCmd>(connection, mediator);
    }
}