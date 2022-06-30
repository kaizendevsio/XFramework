using IdentityServer.Core.DataAccess.Query.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetLocationListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetLocationListRequest, GetLocationListQuery, List<IdentityLocationResponse>>(connection, mediator);
    }
}