using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Create;

public class CreatePatientHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePatientRequest, CreatePatientCmd>(connection, mediator);
    }
}