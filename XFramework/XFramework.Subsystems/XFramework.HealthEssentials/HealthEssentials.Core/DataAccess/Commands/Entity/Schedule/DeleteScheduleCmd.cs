using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

public class DeleteScheduleCmd : DeleteScheduleRequest, IRequest<CmdResponse<DeleteScheduleCmd>>
{
    
}