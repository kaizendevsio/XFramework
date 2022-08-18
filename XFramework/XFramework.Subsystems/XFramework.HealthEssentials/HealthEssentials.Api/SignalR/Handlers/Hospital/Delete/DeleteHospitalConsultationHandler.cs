using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Delete;

public class DeleteHospitalConsultationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteHospitalConsultationRequest, DeleteHospitalConsultationCmd>(connection, mediator);
    }
}