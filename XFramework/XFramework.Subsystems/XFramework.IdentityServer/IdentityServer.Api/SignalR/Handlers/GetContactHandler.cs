using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetContactRequest, GetContactQuery>(connection, mediator);
    }
}