using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

public class CreateScheduleCmd : CreateScheduleRequest, IRequest<CmdResponse<CreateScheduleRequest>>
{
    
}