using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryTagHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryTagCmd, CmdResponse<CreateLaboratoryTagCmd>>
{
    public CreateLaboratoryTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryTagCmd>> Handle(CreateLaboratoryTagCmd request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryGuid}", CancellationToken.None);
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.LaboratoryGuid} does not exist",
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

        var laboratoryTag = request.Adapt<LaboratoryTag>();
        laboratoryTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        laboratoryTag.Laboratory = laboratory;
        laboratoryTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryTags.AddAsync(laboratoryTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(laboratoryTag.Guid);
        return new()
        {
            Message = $"Laboratory Tag with Guid {laboratoryTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };

    }
}