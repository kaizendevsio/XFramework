using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticJobOrderCmd, CmdResponse<UpdateLogisticJobOrderCmd>>
{
    public UpdateLogisticJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLogisticJobOrderCmd>> Handle(UpdateLogisticJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrder == null)
        {
            return new()
            {
                Message = $"Logistic Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedJobOrder = request.Adapt(existingJobOrder);

        if (request.RiderGuid is not null)
        {
            var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders.FirstOrDefaultAsync(x => x.Guid == $"{request.RiderGuid}", CancellationToken.None);
            if (rider is null)
            {
                return new()
                {
                    Message = $"Rider with guid {request.RiderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrder.Rider = rider;
        }

        if (request.ScheduleGuid is not null)
        {
            var schedule = await _dataLayer.HealthEssentialsContext.Schedules.FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
            if (schedule is null)
            {
                return new()
                {
                    Message = $"Schedule with guid {request.ScheduleGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrder.Schedule = schedule;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Job Order with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}