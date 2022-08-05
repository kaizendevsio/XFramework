using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Delete;

public class DeletePatientHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeletePatientRequest, DeletePatientCmd>(connection, mediator);
    }
}