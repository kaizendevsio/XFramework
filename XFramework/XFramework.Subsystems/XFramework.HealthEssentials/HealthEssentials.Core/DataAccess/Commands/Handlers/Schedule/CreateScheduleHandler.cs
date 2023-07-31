using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Schedule;

public class CreateScheduleHandler : CommandBaseHandler, IRequestHandler<CreateScheduleCmd, CmdResponse<CreateScheduleCmd>>
{
    public CreateScheduleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateScheduleCmd>> Handle(CreateScheduleCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.ScheduleEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Schedule with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var priority = await _dataLayer.HealthEssentialsContext.SchedulePriorities.FirstOrDefaultAsync(x => x.Guid == $"{request.PriorityGuid}", CancellationToken.None);
        if (priority is null)
        {
            return new ()
            {
                Message = $"Schedule Priority with Guid {request.PriorityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var schedule = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Schedule>();
        schedule.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        schedule.Priority = priority;
        schedule.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Schedules.AddAsync(schedule, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(schedule.Guid);
        return new()
        {
            Message = $"Schedule with Guid {schedule.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}