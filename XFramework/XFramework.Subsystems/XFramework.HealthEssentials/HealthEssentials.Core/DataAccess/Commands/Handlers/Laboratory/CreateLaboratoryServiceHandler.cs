using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryServiceCmd, CmdResponse<CreateLaboratoryServiceCmd>>
{
    public CreateLaboratoryServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryServiceCmd>> Handle(CreateLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
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

        var service = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryService>();
        service.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        service.Entity = serviceEntity;
        service.Unit = unit;
        service.Laboratory = laboratory;
        service.LaboratoryLocation = laboratoryLocation;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryServices.AddAsync(service,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(service.Guid);
        return new()
        {
            Message = $"Laboratory service with Guid {service.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}