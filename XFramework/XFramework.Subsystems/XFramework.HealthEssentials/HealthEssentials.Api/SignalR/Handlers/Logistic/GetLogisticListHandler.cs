using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class GetLogisticListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetLogisticListRequest, GetLogisticListQuery>(connection, mediator);
    }
}