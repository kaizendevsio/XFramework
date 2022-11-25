using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticJobOrderDetailCmd, CmdResponse<UpdateLogisticJobOrderDetailCmd>>
{
    public UpdateLogisticJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLogisticJobOrderDetailCmd>> Handle(UpdateLogisticJobOrderDetailCmd request, CancellationToken cancellationToken)
    {
        var existingDetail = await _dataLayer.HealthEssentialsContext.LogisticJobOrderDetails.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDetail == null)
        {
            return new()
            {
                Message = $"Logistic Job Order Detail with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDetail = request.Adapt(existingDetail);

        if (request.LocationGuid is not null)
        {
            var location = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LocationGuid}", CancellationToken.None);
            if (location is null)
            {
                return new()
                {
                    Message = $"Location with Guid {request.LocationGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDetail.LocationGuid = location.Guid;
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
            updatedDetail.Unit = unit;
        }

        if (request.LogisticJobOrderGuid is not null)
        {
            var jobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LogisticJobOrderGuid}", CancellationToken.None);
            if (jobOrder is null)
            {
                return new()
                {
                    Message = $"Job Order with Guid {request.LogisticJobOrderGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDetail.LogisticJobOrder = jobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Job Order Detail with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}