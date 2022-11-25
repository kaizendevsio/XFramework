using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Get;

public class GetLogisticRiderTagListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetLogisticRiderTagListRequest, GetLogisticRiderTagListQuery, List<LogisticRiderTagResponse>>(connection, mediator);
    }
}