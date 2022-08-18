using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Delete;

public class DeletePatientTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeletePatientTagRequest, DeletePatientTagCmd>(connection, mediator);
    }
}