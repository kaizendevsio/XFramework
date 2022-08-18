using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Create;

public class CreatePatientAilmentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePatientAilmentRequest, CreatePatientAilmentCmd>(connection, mediator);
    }
}