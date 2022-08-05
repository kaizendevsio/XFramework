using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

public class CreateScheduleCmd : CreateScheduleRequest, IRequest<CmdResponse<CreateScheduleCmd>>
{
    
}