using HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Update;

public class UpdateVendorEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateVendorEntityGroupRequest, UpdateVendorEntityGroupCmd>(connection, mediator);
    }
}