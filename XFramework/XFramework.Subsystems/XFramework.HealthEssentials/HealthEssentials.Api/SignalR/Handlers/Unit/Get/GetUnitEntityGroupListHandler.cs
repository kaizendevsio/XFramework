using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Get;

public class GetUnitEntityGroupListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetUnitEntityGroupListRequest, GetUnitEntityGroupListQuery, List<UnitEntityGroupResponse>>(connection, mediator);
    }
}