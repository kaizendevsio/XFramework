using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Update;

public class UpdateHospitalConsultationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateHospitalConsultationRequest, UpdateHospitalConsultationCmd>(connection, mediator);
    }
}