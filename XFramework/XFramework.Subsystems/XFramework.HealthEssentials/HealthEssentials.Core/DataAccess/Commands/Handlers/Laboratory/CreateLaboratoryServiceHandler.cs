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
        var type = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.TypeGuid}", cancellationToken: cancellationToken);
       
        if (type is null)
        {
            return new ()
            {
                Message = $"Service type with Guid {request.TypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(i => i.Guid == $"{request.UnitGuid}", cancellationToken: cancellationToken);

        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryService>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = type.Id;
        entity.Unit = unit;
        
        _dataLayer.HealthEssentialsContext.LaboratoryServices.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}