using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class VerifyLogisticIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<VerifyLogisticIdentityRequest, VerifyLogisticIdentityQuery, IdentityValidationResponse>(connection, mediator);
    }
}