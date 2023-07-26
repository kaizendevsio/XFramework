using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryLocationTagHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryLocationTagCmd, CmdResponse<CreateLaboratoryLocationTagCmd>>
{
    public CreateLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryLocationTagCmd>> Handle(CreateLaboratoryLocationTagCmd request, CancellationToken cancellationToken)
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
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var locationTag = request.Adapt<LaboratoryLocationTag>();
        locationTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        locationTag.LaboratoryLocation = location;
        locationTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryLocationTags.AddAsync(locationTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(locationTag.Guid);
        return new ()
        {
            Message = $"Laboratory Location Tag with Guid {locationTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}