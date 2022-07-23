using HealthEssentials.Core.DataAccess.Commands.Entity.Schedule;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Schedule;

public class DeleteScheduleHandler : CommandBaseHandler, IRequestHandler<DeleteScheduleCmd, CmdResponse<DeleteScheduleCmd>>
{
    public DeleteScheduleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteScheduleCmd>> Handle(DeleteScheduleCmd request, CancellationToken cancellationToken)
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
        
        existingSchedule.IsDeleted = true;
        existingSchedule.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingSchedule);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Schedule with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}