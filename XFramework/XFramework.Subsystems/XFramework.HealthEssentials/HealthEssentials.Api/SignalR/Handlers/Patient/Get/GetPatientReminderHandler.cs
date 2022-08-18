using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Get;

public class GetPatientReminderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetPatientReminderRequest, GetPatientReminderQuery, PatientReminderResponse>(connection, mediator);
    }
}