using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Schedule;

public class UpdateScheduleHandler : CommandBaseHandler, IRequestHandler<UpdateScheduleCmd, CmdResponse<UpdateScheduleCmd>>
{
    public UpdateScheduleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateScheduleCmd>> Handle(UpdateScheduleCmd request, CancellationToken cancellationToken)
    {
        var existingSchedule = await _dataLayer.HealthEssentialsContext.Schedules.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingSchedule == null)
        {
            return new()
            {
                Message = $"Schedule with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedSchedule = request.Adapt(existingSchedule);

        if (request.EntityGuid is null)
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
            updatedSchedule.Type = entity;
        }

        if (request.PriorityGuid is null)
        {
            var priority = await _dataLayer.HealthEssentialsContext.SchedulePriorities.FirstOrDefaultAsync(x => x.Guid == $"{request.PriorityGuid}", CancellationToken.None);
            if (priority is null)
            {
                return new ()
                {
                    Message = $"Schedule Priority with Guid {request.PriorityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedSchedule.Priority = priority;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedSchedule);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Schedule with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}