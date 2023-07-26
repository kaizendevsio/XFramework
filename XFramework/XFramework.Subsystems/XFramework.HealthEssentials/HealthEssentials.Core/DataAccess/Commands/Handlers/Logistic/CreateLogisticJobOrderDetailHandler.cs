using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<CreateLogisticJobOrderDetailCmd, CmdResponse<CreateLogisticJobOrderDetailCmd>>
{
    public CreateLogisticJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticJobOrderDetailCmd>> Handle(CreateLogisticJobOrderDetailCmd request, CancellationToken cancellationToken)
    {
        var detail = request.Adapt<LogisticJobOrderDetail>();
        
        var location = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LocationGuid}", CancellationToken.None);
        if (location is null)
        {
            return new ()
            {
                Message = $"Location with Guid {request.LocationGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        if (request.UnitGuid is not null)
        {
            var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
            if (unit is null)
            {
                return new()
                {
                    Message = $"Unit with Guid {request.UnitGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            detail.Unit = unit;
        }

        var jobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LogisticJobOrderGuid}", CancellationToken.None);
        if (jobOrder is null)
        {
            return new()
            {
                Message = $"Job Order with Guid {request.LogisticJobOrderGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        detail.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        detail.LogisticJobOrder = jobOrder;
        detail.LocationGuid = location.Guid;
        
        await _dataLayer.HealthEssentialsContext.LogisticJobOrderDetails.AddAsync(detail, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(detail.Guid);
        return new()
        {
            Message = $"Logistic Job Order Detail with Guid {detail.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}