using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Update;

public class UpdatePharmacyEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdatePharmacyEntityRequest, UpdatePharmacyEntityCmd>(connection, mediator);
    }
}