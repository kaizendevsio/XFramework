using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateContactRequest, CreateContactCmd>(connection, mediator);
    }
}