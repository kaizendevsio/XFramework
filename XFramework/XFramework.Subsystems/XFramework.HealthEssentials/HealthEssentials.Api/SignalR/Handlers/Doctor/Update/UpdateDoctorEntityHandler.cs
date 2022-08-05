using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Update;

public class UpdateDoctorEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateDoctorEntityRequest, UpdateDoctorEntityCmd>(connection, mediator);
    }
}