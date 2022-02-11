using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetContactListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetContactListRequest, GetContactListQuery>(connection, mediator);
    }
}