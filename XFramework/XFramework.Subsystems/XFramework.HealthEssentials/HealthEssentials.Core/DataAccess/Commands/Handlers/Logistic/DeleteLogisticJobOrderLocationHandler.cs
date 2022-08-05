using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticJobOrderLocationHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticJobOrderLocationCmd, CmdResponse<DeleteLogisticJobOrderLocationCmd>>
{
    public DeleteLogisticJobOrderLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLogisticJobOrderLocationCmd>> Handle(DeleteLogisticJobOrderLocationCmd request, CancellationToken cancellationToken)
    {
        var existingLocation = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLocation == null)
        {
            return new()
            {
                Message = $"Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLocation.IsDeleted = true;
        existingLocation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Location with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}