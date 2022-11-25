using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Medicine.Update;

public class UpdateMedicineIntakeEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateMedicineIntakeEntityRequest, UpdateMedicineIntakeEntityCmd>(connection, mediator);
    }
}