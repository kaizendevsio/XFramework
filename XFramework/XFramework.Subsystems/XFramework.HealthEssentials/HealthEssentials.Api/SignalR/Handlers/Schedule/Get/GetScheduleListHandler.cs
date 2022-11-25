using HealthEssentials.Core.DataAccess.Query.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Api.SignalR.Handlers.Schedule.Get;

public class GetScheduleListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetScheduleListRequest, GetScheduleListQuery, List<ScheduleResponse>>(connection, mediator);
    }
}