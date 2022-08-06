using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy.Delete;

public class DeletePharmacyTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeletePharmacyTagRequest, DeletePharmacyTagCmd>(connection, mediator);
    }
}