using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class VerifyLaboratoryIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<VerifyLaboratoryIdentityRequest, VerifyLaboratoryIdentityQuery, IdentityValidationResponse>(connection, mediator);
    }
}