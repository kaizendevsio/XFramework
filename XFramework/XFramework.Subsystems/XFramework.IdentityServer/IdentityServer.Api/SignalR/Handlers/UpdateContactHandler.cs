using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Api.SignalR.Handlers;

public class UpdateContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateContactRequest, UpdateContactCmd>(connection, mediator);
    }
}