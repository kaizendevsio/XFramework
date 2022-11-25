using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Schedule.Delete;

public class DeleteScheduleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteScheduleRequest, DeleteScheduleCmd>(connection, mediator);
    }
}