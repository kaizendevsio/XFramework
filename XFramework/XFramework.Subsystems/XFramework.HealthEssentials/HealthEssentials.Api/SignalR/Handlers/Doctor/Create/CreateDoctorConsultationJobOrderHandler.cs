using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Create;

public class CreateDoctorConsultationJobOrderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateDoctorConsultationJobOrderRequest, CreateDoctorConsultationJobOrderCmd>(connection, mediator);
    }
}