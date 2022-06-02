using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class UpdateLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateLaboratoryRequest, UpdateLaboratoryCmd>(connection, mediator);
    }
}