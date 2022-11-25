using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticJobOrderCmd, CmdResponse<DeleteLogisticJobOrderCmd>>
{
    public DeleteLogisticJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLogisticJobOrderCmd>> Handle(DeleteLogisticJobOrderCmd request, CancellationToken cancellationToken)
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
        
        existingJobOrder.IsDeleted = true;
        existingJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}