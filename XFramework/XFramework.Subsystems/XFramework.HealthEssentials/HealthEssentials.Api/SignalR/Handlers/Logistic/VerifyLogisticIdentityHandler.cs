using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class VerifyLogisticIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<VerifyLogisticIdentityRequest, VerifyLogisticIdentityQuery>(connection, mediator);
    }
}