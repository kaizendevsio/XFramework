using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Get;

public class GetVendorEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetVendorEntityGroupRequest, GetVendorEntityGroupQuery, VendorEntityGroupResponse>(connection, mediator);
    }
}