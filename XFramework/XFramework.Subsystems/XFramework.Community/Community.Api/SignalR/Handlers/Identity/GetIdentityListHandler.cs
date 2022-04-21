using Community.Core.DataAccess.Query.Entity.Identity;
using Community.Domain.Generic.Contracts.Requests.Get;

namespace Community.Api.SignalR.Handlers.Identity;

public class GetIdentityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetIdentityListRequest, GetIdentityListQuery>(connection, mediator);
    }
}