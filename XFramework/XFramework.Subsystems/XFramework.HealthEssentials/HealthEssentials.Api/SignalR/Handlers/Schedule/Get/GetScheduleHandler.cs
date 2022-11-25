using HealthEssentials.Core.DataAccess.Query.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Api.SignalR.Handlers.Schedule.Get;

public class GetScheduleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetScheduleRequest, GetScheduleQuery, ScheduleResponse>(connection, mediator);
    }
}