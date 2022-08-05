using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Delete;

public class DeletePharmacyJobOrderConsultationJobOrderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeletePharmacyJobOrderConsultationJobOrderRequest, DeletePharmacyJobOrderConsultationJobOrderCmd>(connection, mediator);
    }
}