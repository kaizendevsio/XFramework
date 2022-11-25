using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityCmd, CmdResponse<UpdateLaboratoryServiceEntityCmd>>
{
    public UpdateLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityCmd>> Handle(UpdateLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryServiceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryServiceEntity is null)
        {
            return new ()
            {
                Message = $"Laboratory Service entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLaboratoryServiceEntity = request.Adapt(existingLaboratoryServiceEntity);

        if (request.GroupGuid is null)
        {
            var serviceEntityGroup = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups.FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (serviceEntityGroup is null)
            {
                return new ()
                {
                    Message = $"Service entity group with Guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryServiceEntity.Group = serviceEntityGroup;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedLaboratoryServiceEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            Message = $"Laboratory service entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}