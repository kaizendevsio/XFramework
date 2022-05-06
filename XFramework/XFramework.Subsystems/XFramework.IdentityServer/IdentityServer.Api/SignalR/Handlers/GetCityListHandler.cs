using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetCityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetCityListRequest, GetCityListQuery>(connection, mediator);
    }
}