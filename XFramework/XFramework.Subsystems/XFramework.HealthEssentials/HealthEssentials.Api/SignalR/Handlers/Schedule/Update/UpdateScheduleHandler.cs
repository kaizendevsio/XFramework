using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Schedule.Update;

public class UpdateScheduleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateScheduleRequest, UpdateScheduleCmd>(connection, mediator);
    }
}