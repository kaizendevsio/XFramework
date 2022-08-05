using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Update;

public class UpdateDoctorConsultationJobOrderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateDoctorConsultationJobOrderRequest, UpdateDoctorConsultationJobOrderCmd>(connection, mediator);
    }
}