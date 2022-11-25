using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryJobOrderResultFileHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryJobOrderResultFileCmd, CmdResponse<UpdateLaboratoryJobOrderResultFileCmd>>
{
    public UpdateLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryJobOrderResultFileCmd>> Handle(UpdateLaboratoryJobOrderResultFileCmd request, CancellationToken cancellationToken)
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
        var updatedResultFile = request.Adapt(existingResultFile);

        if (request.LaboratoryJobOrderResultGuid is null)
        {
            var jobOrderResult = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryJobOrderResultGuid}", CancellationToken.None);
            if (jobOrderResult is null)
            {
                return new ()
                {
                    Message = $"Laboratory Job Order Result with Guid {request.LaboratoryJobOrderResultGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedResultFile.LaboratoryJobOrderResult = jobOrderResult;
        }

        if (request.StorageFileGuid is null)
        {
            var storageFile = await _dataLayer.XnelSystemsContext.StorageFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.StorageFileGuid}", CancellationToken.None);
            if (storageFile is null)
            {
                return new ()
                {
                    Message = $"Storage File with Guid {request.StorageFileGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedResultFile.StorageFileId = storageFile.Id;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedResultFile);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory Job Order Result File with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}