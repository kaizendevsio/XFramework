using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityCmd, CmdResponse<UpdateLaboratoryServiceEntityCmd>>
{
    public UpdateLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityCmd>> Handle(UpdateLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryServiceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingLaboratoryServiceEntity is null)
        {
            return new ()
            {
                Message = $"Laboratory Service entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var serviceEntityGroup = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", cancellationToken: cancellationToken);
       
        if (serviceEntityGroup is null)
        {
            return new ()
            {
                Message = $"Service type group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedLaboratoryServiceEntity = request.Adapt(existingLaboratoryServiceEntity);
        updatedLaboratoryServiceEntity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updatedLaboratoryServiceEntity.Group = serviceEntityGroup;
        
        _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.Update(updatedLaboratoryServiceEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service entity with Guid {updatedLaboratoryServiceEntity.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedLaboratoryServiceEntity.Guid)
            }
        };
    }
}