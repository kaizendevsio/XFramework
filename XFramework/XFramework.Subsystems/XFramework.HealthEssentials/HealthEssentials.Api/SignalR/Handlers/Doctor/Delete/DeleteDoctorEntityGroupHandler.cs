using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Delete;

public class DeleteDoctorEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteDoctorEntityGroupRequest, DeleteDoctorEntityGroupCmd>(connection, mediator);
    }
}