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
            .FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", cancellationToken: cancellationToken);
       
        if (serviceEntity is null)
        {
            return new ()
            {
                Message = $"Service type with Guid {request.EntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(i => i.Guid == $"{request.UnitGuid}", cancellationToken: cancellationToken);

        var updatedLaboratoryService = request.Adapt(existingLaboratoryService);
        updatedLaboratoryService.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updatedLaboratoryService.Entity = serviceEntity;
        updatedLaboratoryService.Unit = unit;
        
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