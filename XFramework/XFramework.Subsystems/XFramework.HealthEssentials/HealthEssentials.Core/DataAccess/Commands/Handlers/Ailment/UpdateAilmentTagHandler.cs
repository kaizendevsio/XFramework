using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentTagHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentTagCmd, CmdResponse<UpdateAilmentTagCmd>>
{
    public UpdateAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentTagCmd>> Handle(UpdateAilmentTagCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentTag = await _dataLayer.HealthEssentialsContext.AilmentTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingAilmentTag is null)
        {
            return new ()
            {
                Message = $"Ailment Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedAilmentTag = request.Adapt(existingAilmentTag);

        if (request.AilmentGuid is not null)
        { 
            var ailment = await _dataLayer.HealthEssentialsContext.Ailments.FirstOrDefaultAsync(x => x.Guid == $"{request.AilmentGuid}", CancellationToken.None); 
            if (ailment == null)
            {
                return new()
                {
                    Message = $"Ailment with Guid {request.AilmentGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedAilmentTag.Ailment = ailment;
        }

        if (request.TagGuid is not null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag == null)
            {
                return new ()
                {
                    Message = $"Ailment with Guid {request.TagGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedAilmentTag.Tag = tag;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedAilmentTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}