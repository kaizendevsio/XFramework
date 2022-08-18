using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Get;

public class GetPatientEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetPatientEntityRequest, GetPatientEntityQuery, PatientEntityResponse>(connection, mediator);
    }
}