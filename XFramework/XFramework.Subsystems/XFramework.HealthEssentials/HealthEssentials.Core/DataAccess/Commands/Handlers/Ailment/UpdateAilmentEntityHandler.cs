using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentEntityHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentEntityCmd, CmdResponse<UpdateAilmentEntityCmd>>
{
    public UpdateAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentEntityCmd>> Handle(UpdateAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentEntity = await _dataLayer.HealthEssentialsContext.AilmentEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingAilmentEntity is null)
        {
            return new ()
            {
                Message = $"Ailment Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedAilmentEntity = request.Adapt(existingAilmentEntity);

        if (request.GroupGuid is not null)
        { 
            var ailmentEntityGroup = await _dataLayer.HealthEssentialsContext.AilmentEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (ailmentEntityGroup == null)
            {
                return new()
                {
                    Message = $"Ailment entity group with Guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedAilmentEntity.Group = ailmentEntityGroup;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedAilmentEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}