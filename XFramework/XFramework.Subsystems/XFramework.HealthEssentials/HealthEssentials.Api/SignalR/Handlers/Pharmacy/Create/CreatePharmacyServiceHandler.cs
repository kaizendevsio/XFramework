using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Create;

public class CreatePharmacyServiceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePharmacyServiceRequest, CreatePharmacyServiceCmd>(connection, mediator);
    }
}