using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceCmd, CmdResponse<UpdateLaboratoryServiceCmd>>
{
    public UpdateLaboratoryServiceHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceCmd>> Handle(UpdateLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryService is null)
        {
            return new ()
            {
                Message = $"Laboratory Service with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLaboratoryService = request.Adapt(existingLaboratoryService);

        if (request.EntityGuid is null)
        {
            var serviceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", cancellationToken: cancellationToken);
            if (serviceEntity is null)
            {
                return new ()
                {
                    Message = $"Service entity with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryService.Entity = serviceEntity;
        }

        if (request.LaboratoryGuid is null)
        {
            var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryGuid}", CancellationToken.None);
            if (laboratory is null)
            {
                return new ()
                {
                    Message = $"Laboratory with Guid {request.LaboratoryGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryService.Laboratory = laboratory;
        }

        if (request.LaboratoryLocationGuid is null)
        {
            var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryLocationGuid}", CancellationToken.None);
            if (laboratoryLocation is null)
            {
                return new ()
                {
                    Message = $"Laboratory location with Guid {request.LaboratoryLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryService.LaboratoryLocation = laboratoryLocation;
        }

        if (request.UnitGuid is null)
        {
            var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(i => i.Guid == $"{request.UnitGuid}", CancellationToken.None);
            if (unit is null)
            {
                return new ()
                {
                    Message = $"Unit with Guid {request.UnitGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryService.Unit = unit;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedLaboratoryService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            Message = $"Laboratory service with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}