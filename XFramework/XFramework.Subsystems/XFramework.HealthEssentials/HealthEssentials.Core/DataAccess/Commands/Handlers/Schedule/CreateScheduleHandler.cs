using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Schedule;

public class CreateScheduleHandler : CommandBaseHandler, IRequestHandler<CreateScheduleCmd, CmdResponse<ScheduleResponse>>
{
    public CreateScheduleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<ScheduleResponse>> Handle(CreateScheduleCmd request, CancellationToken cancellationToken)
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

        var schedule = request.Adapt<Domain.Generics.Contracts.Schedule>();
        schedule.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        schedule.Priority = priority;
        schedule.Type = entity;
        
        await _dataLayer.HealthEssentialsContext.Schedules.AddAsync(schedule, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Schedule with Guid {schedule.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = schedule.Adapt<ScheduleResponse>()
        };
    }
}