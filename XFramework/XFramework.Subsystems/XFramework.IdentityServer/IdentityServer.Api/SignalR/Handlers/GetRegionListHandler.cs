using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetRegionListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetRegionListRequest, GetRegionListQuery>(connection, mediator);
    }
}