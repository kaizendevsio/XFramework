using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;

namespace HealthEssentials.Api.SignalR.Handlers.Pharmacy;

public class VerifyPharmacyIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<VerifyPharmacyIdentityRequest, VerifyPharmacyIdentityQuery>(connection, mediator);
    }
}