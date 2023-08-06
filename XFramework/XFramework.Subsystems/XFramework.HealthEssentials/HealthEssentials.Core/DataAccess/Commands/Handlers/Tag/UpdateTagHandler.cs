using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class UpdateTagHandler : CommandBaseHandler, IRequestHandler<UpdateTagCmd, CmdResponse<UpdateTagCmd>>
{
    public UpdateTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateTagCmd>> Handle(UpdateTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag == null)
        {
            return new()
            {
                Message = $"Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedTag = request.Adapt(existingTag);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.TagEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedTag.Type = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}