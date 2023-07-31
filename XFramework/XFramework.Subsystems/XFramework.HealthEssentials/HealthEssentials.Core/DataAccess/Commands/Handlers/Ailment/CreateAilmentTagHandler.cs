using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentTagHandler : CommandBaseHandler, IRequestHandler<CreateAilmentTagCmd, CmdResponse<CreateAilmentTagCmd>>
{
    public CreateAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentTagCmd>> Handle(CreateAilmentTagCmd request, CancellationToken cancellationToken)
    {
        var ailment = await _dataLayer.HealthEssentialsContext.Ailments.FirstOrDefaultAsync(x => x.Guid == $"{request.AilmentGuid}", CancellationToken.None);
        if (ailment == null)
        {
            return new ()
            {
                Message = $"Ailment with Guid {request.AilmentGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag == null)
        {
            return new ()
            {
                Message = $"Ailment with Guid {request.TagGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var ailmentTag = request.Adapt<AilmentTag>();
        ailmentTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        ailmentTag.Ailment = ailment;
        ailmentTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.AilmentTags.AddAsync(ailmentTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(ailmentTag.Guid);
        return new()
        {
            Message = $"Ailment Tag with Guid {ailmentTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };

    }
}