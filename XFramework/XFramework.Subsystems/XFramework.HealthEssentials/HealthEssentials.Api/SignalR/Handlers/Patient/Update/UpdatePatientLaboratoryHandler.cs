using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Update;

public class UpdatePatientLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdatePatientLaboratoryRequest, UpdatePatientLaboratoryCmd>(connection, mediator);
    }
}