using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryJobOrderResultFileHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryJobOrderResultFileCmd, CmdResponse<CreateLaboratoryJobOrderResultFileCmd>>
{
    public CreateLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryJobOrderResultFileCmd>> Handle(CreateLaboratoryJobOrderResultFileCmd request, CancellationToken cancellationToken)
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
        
        var storageFile = await _dataLayer.XnelSystemsContext.StorageFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.StorageFileGuid}", CancellationToken.None);
        if (storageFile is null)
        {
            return new ()
            {
                Message = $"Storage File with Guid {request.StorageFileGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var resultFile = request.Adapt<LaboratoryJobOrderResultFile>();
        resultFile.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        resultFile.LaboratoryJobOrderResult = jobOrderResult;
        resultFile.StorageFileId = storageFile.Id;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResultFiles.AddAsync(resultFile, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(resultFile.Guid);
        return new()
        {
            Message = $"Laboratory Job Order Result File with Guid {resultFile.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}