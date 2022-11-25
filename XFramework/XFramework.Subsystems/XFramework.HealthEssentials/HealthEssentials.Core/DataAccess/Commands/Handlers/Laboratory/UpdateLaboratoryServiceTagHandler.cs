using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceTagHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceTagCmd, CmdResponse<UpdateLaboratoryServiceTagCmd>>
{
    public UpdateLaboratoryServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryServiceTagCmd>> Handle(UpdateLaboratoryServiceTagCmd request, CancellationToken cancellationToken)
    {
        var existingServiceTag = await _dataLayer.HealthEssentialsContext.LaboratoryServiceTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingServiceTag is null)
        {
            return new ()
            {
                Message = $"Laboratory Service Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedServiceTag = request.Adapt(existingServiceTag);

        if (request.LaboratoryServiceGuid is null)
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
            updatedServiceTag.LaboratoryService = service;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with guid {request.TagGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedServiceTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Service Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}