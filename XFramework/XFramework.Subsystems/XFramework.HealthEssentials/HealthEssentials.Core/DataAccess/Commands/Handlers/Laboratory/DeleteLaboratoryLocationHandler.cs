using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryLocationHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryLocationCmd, CmdResponse<DeleteLaboratoryLocationCmd>>
{
    public DeleteLaboratoryLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryLocationCmd>> Handle(DeleteLaboratoryLocationCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryLocation is null)
        {
            return new ()
            {
                Message = $"Laboratory Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLaboratoryLocation.IsDeleted = true;
        existingLaboratoryLocation.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Location with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
        
    }
}