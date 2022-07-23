using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryLocationTagHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryLocationTagCmd, CmdResponse<UpdateLaboratoryLocationTagCmd>>
{
    public UpdateLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryLocationTagCmd>> Handle(UpdateLaboratoryLocationTagCmd request, CancellationToken cancellationToken)
    {
        var existingLocationTag = await _dataLayer.HealthEssentialsContext.LaboratoryLocationTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLocationTag is null)
        {
            return new ()
            {
                Message = $"Laboratory Location Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLocationTag = request.Adapt(existingLocationTag);

        if (request.LaboratoryLocationGuid is null)
        {
            var location = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryLocationGuid}", CancellationToken.None);
            if (location is null)
            {
                return new ()
                {
                    Message = $"Laboratory Location with Guid {request.LaboratoryLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocationTag.LaboratoryLocation = location;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.TagGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocationTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedLocationTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Laboratory Location Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}