using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

public class UpdateScheduleCmd : UpdateScheduleRequest, IRequest<CmdResponse<UpdateScheduleCmd>>
{
    
}