using System.Security.AccessControl;
using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class UpdateTagEntityHandler : CommandBaseHandler, IRequestHandler<UpdateTagEntityCmd, CmdResponse<UpdateTagEntityCmd>>
{
    public UpdateTagEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateTagEntityCmd>> Handle(UpdateTagEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.TagEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity == null)
        {
            return new()
            {
                Message = $"Tag Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedEntity = request.Adapt(existingEntity);

        if (request.GroupGuid is null)
        {
            var group = await _dataLayer.HealthEssentialsContext.TagEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (group is null)
            {
                return new ()
                {
                    Message = $"Tag group with guid {request.GroupGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.Group = group;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Tag Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}