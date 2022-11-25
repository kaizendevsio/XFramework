using HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Update;

public class UpdateVendorHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateVendorRequest, UpdateVendorCmd>(connection, mediator);
    }
}