using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient;

public class VerifyPatientIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<VerifyPatientIdentityRequest, VerifyPatientIdentityQuery>(connection, mediator);
    }
}