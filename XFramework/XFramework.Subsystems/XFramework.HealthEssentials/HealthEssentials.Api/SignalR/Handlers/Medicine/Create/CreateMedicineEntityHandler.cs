using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Medicine.Create;

public class CreateMedicineEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateMedicineEntityRequest, CreateMedicineEntityCmd>(connection, mediator);
    }
}