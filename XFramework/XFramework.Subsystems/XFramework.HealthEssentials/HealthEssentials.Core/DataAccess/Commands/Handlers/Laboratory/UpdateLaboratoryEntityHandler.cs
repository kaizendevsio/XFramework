using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryEntityHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryEntityCmd, CmdResponse<UpdateLaboratoryEntityCmd>>
{
    public UpdateLaboratoryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryEntityCmd>> Handle(UpdateLaboratoryEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.LaboratoryEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Laboratory Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedEntity = request.Adapt(existingEntity);

        if (request.GroupGuid is null)
        {
            var group = await _dataLayer.HealthEssentialsContext.LaboratoryEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (group is null)
            {
                return new ()
                {
                    Message = $"Laboratory Entity Group with Guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.Group = group;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}