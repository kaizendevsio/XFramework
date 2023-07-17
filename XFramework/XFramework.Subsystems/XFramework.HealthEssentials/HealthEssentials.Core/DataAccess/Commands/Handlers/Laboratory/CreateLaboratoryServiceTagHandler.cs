using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryServiceTagHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryServiceTagCmd, CmdResponse<CreateLaboratoryServiceTagCmd>>
{
    public CreateLaboratoryServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryServiceTagCmd>> Handle(CreateLaboratoryServiceTagCmd request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryServiceGuid}", CancellationToken.None);
        if (service is null)
        {
            return new ()
            {
                Message = $"Laboratory service with guid {request.LaboratoryServiceGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with guid {request.TagGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var serviceTag = request.Adapt<LaboratoryServiceTag>();
        serviceTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        serviceTag.LaboratoryService = service;
        serviceTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryServiceTags.AddAsync(serviceTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(serviceTag.Guid);
        return new()
        {
            Message = $"Laboratory service tag with guid {serviceTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
           
        };
        
    }
}