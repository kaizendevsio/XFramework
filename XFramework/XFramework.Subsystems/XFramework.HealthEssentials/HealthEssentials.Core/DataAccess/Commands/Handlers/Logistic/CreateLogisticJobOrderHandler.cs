using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticJobOrderHandler : CommandBaseHandler, IRequestHandler<CreateLogisticJobOrderCmd, CmdResponse<CreateLogisticJobOrderCmd>>
{
    public CreateLogisticJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticJobOrderCmd>> Handle(CreateLogisticJobOrderCmd request, CancellationToken cancellationToken)
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
        
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules.FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
        if (schedule is null)
        {
            return new()
            {
                Message = $"Schedule with guid {request.ScheduleGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrder = request.Adapt<LogisticJobOrder>();
        jobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrder.Rider = rider;
        jobOrder.Schedule = schedule;

        await _dataLayer.HealthEssentialsContext.LogisticJobOrders.AddAsync(jobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrder.Guid);
        return new()
        {
            Message = $"Logistic job order with guid {jobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}