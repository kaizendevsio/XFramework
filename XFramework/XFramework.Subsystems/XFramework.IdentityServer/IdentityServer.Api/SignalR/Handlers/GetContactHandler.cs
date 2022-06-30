using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetContactHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetContactRequest, GetContactQuery, ContactResponse>(connection, mediator);
    }
}