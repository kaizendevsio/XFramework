using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient;

public class GetPatientIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetPatientIdentityRequest, GetPatientIdentityQuery>(connection, mediator);
    }
}