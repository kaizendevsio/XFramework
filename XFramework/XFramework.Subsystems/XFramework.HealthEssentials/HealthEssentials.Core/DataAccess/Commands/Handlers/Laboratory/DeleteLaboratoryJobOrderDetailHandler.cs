using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderDetailCmd, CmdResponse<DeleteLaboratoryJobOrderDetailCmd>>
{
    public DeleteLaboratoryJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderDetailCmd>> Handle(DeleteLaboratoryJobOrderDetailCmd request, CancellationToken cancellationToken)
    {
        var existingDetail = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderDetails.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDetail is null)
        {
            return new ()
            {
                Message = $"Laboratory Job Order Detail with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDetail.IsDeleted = true;
        existingDetail.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Job Order Detail with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}