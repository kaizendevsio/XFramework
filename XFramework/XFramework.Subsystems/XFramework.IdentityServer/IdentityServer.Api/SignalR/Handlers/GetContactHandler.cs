using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetContactRequest, GetContactQuery>(connection, mediator);
    }
}