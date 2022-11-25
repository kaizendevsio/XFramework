using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Verify;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Verify;

public class VerifyDoctorHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<VerifyDoctorRequest, VerifyDoctorQuery, IdentityValidationResponse>(connection, mediator);
    }
}