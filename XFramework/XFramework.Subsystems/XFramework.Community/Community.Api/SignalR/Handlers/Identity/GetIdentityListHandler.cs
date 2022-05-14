using Community.Core.DataAccess.Query.Entity.Identity;
using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Api.SignalR.Handlers.Identity;

public class GetIdentityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetIdentityListRequest, GetIdentityListQuery, List<CommunityIdentityResponse>>(connection, mediator);
    }
}