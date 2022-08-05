using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Delete;

public class DeletePatientConsultationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeletePatientConsultationRequest, DeletePatientConsultationCmd>(connection, mediator);
    }
}