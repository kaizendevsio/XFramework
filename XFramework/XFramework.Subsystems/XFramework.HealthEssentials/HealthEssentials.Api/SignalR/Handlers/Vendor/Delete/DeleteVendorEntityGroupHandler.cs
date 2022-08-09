using HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Delete;

public class DeleteVendorEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteVendorEntityGroupRequest, DeleteVendorEntityGroupCmd>(connection, mediator);
    }
}