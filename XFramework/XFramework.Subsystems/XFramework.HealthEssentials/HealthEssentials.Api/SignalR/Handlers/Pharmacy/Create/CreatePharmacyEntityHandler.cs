using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Create;

public class CreatePharmacyEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePharmacyEntityRequest, CreatePharmacyEntityCmd>(connection, mediator);
    }
}