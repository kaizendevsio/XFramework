using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetProvinceListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetProvinceListRequest, GetProvinceListQuery>(connection, mediator);
    }
}