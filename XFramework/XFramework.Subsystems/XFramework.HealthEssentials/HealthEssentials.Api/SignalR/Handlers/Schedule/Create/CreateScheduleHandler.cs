using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Create;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Api.SignalR.Handlers.Schedule.Create;

public class CreateScheduleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateScheduleRequest, CreateScheduleCmd, ScheduleResponse>(connection, mediator);
    }
}