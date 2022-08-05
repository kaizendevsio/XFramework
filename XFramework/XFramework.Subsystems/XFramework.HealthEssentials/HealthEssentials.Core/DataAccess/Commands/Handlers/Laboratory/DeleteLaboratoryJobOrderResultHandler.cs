using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderResultHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderResultCmd, CmdResponse<DeleteLaboratoryJobOrderResultCmd>>
{
    public DeleteLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultCmd>> Handle(DeleteLaboratoryJobOrderResultCmd request, CancellationToken cancellationToken)
    {
        var existingResult = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingResult is null)
        {
            return new()
            {
                Message = $"Laboratory Job Order Result with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingResult.IsDeleted = true;
        existingResult.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingResult);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory Job Order Result with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}