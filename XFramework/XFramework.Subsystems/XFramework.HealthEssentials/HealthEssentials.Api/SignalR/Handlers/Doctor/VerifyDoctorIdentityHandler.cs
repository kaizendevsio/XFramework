using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Doctor;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor;

public class VerifyDoctorIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<VerifyDoctorIdentityRequest, VerifyDoctorIdentityQuery>(connection, mediator);
    }
}