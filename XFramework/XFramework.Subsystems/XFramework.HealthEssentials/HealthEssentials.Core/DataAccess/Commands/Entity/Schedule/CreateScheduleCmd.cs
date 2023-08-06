using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Create;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

public class CreateScheduleCmd : CreateScheduleRequest, IRequest<CmdResponse<ScheduleResponse>>
{
    
}