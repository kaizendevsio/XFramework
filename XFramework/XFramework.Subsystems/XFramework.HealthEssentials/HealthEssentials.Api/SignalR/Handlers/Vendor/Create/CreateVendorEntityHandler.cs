using HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Create;

public class CreateVendorEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateVendorEntityRequest, CreateVendorEntityCmd>(connection, mediator);
    }
}