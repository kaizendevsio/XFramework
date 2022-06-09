using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy;

public class VerifyPharmacyIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<VerifyPharmacyMemberRequest, VerifyPharmacyMemberQuery, IdentityValidationResponse>(connection, mediator);
    }
}