using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Update;

public class UpdatePharmacyServiceEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdatePharmacyServiceEntityGroupRequest, UpdatePharmacyServiceEntityGroupCmd>(connection, mediator);
    }
}