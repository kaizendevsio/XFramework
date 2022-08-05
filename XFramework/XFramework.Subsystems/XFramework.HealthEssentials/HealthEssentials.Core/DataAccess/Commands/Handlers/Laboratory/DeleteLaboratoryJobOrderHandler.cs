using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderCmd, CmdResponse<DeleteLaboratoryJobOrderCmd>>
{
    public DeleteLaboratoryJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderCmd>> Handle(DeleteLaboratoryJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrder = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrder is null)
        {
            return new ()
            {
                Message = $"Laboratory Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingJobOrder.IsDeleted = true;
        existingJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}