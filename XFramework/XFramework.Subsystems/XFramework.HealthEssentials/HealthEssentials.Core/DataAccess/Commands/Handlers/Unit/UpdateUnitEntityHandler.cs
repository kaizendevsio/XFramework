using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class UpdateUnitEntityHandler : CommandBaseHandler, IRequestHandler<UpdateUnitEntityCmd, CmdResponse<UpdateUnitEntityCmd>>
{
    public UpdateUnitEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateUnitEntityCmd>> Handle(UpdateUnitEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.UnitEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity == null)
        {
            return new()
            {
                Message = $"Unit Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedEntity = request.Adapt(existingEntity);

        if (request.GroupGuid is null)
        {
            var group = await _dataLayer.HealthEssentialsContext.UnitEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (group is null)
            {
                return new ()
                {
                    Message = $"Group with guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.Group = group;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Unit Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}