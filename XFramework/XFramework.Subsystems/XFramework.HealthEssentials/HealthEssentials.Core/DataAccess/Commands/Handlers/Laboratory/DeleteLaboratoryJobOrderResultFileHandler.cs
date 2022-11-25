using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderResultFileHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderResultFileCmd, CmdResponse<DeleteLaboratoryJobOrderResultFileCmd>>
{
    public DeleteLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultFileCmd>> Handle(DeleteLaboratoryJobOrderResultFileCmd request, CancellationToken cancellationToken)
    {
        var existingResultFile = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResultFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingResultFile is null)
        {
            return new()
            {
                Message = $"Laboratory Job Order Result File with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingResultFile.IsDeleted = true;
        existingResultFile.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingResultFile);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory Job Order Result File with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}