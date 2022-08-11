using HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Update;

public class UpdateVendorEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateVendorEntityRequest, UpdateVendorEntityCmd>(connection, mediator);
    }
}