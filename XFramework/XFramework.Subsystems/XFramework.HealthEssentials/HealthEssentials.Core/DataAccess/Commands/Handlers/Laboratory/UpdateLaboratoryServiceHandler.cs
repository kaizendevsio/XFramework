using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceCmd, CmdResponse<UpdateLaboratoryServiceCmd>>
{
    public UpdateLaboratoryServiceHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceCmd>> Handle(UpdateLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServices
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingLaboratoryService is null)
        {
            return new ()
            {
                Message = $"Laboratory Service with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var serviceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
       
        if (serviceEntity is null)
        {
            return new ()
            {
                Message = $"Service entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (laboratoryLocation is null)
        {
            return new ()
            {
                Message = $"Laboratory location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var unit = await _dataLayer.HealthEssentialsContext.Units
            .FirstOrDefaultAsync(i => i.Guid == $"{request.UnitGuid}", CancellationToken.None);
        
        if (unit is null)
        {
            return new ()
            {
                Message = $"Unit with Guid {request.UnitGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var updatedLaboratoryService = request.Adapt(existingLaboratoryService);
        updatedLaboratoryService.Entity = serviceEntity;
        updatedLaboratoryService.Unit = unit;
        updatedLaboratoryService.Laboratory = laboratory;
        updatedLaboratoryService.LaboratoryLocation = laboratoryLocation;
        
        _dataLayer.HealthEssentialsContext.Update(updatedLaboratoryService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service with Guid {updatedLaboratoryService.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedLaboratoryService.Guid)
            }
        };
    }
}