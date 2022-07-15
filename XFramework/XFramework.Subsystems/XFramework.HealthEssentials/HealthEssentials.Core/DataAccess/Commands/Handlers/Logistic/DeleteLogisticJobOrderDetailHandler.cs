using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticJobOrderDetailCmd, CmdResponse<DeleteLogisticJobOrderDetailCmd>>
{
    public DeleteLogisticJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLogisticJobOrderDetailCmd>> Handle(DeleteLogisticJobOrderDetailCmd request, CancellationToken cancellationToken)
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
        
        existingDetail.IsDeleted = true;
        existingDetail.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Job Order Detail with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}