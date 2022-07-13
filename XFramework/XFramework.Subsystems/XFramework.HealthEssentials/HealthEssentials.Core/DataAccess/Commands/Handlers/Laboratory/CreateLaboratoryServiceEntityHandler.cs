using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryServiceEntityCmd, CmdResponse<CreateLaboratoryServiceEntityCmd>>
{
    public CreateLaboratoryServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryServiceEntityCmd>> Handle(CreateLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", cancellationToken: cancellationToken);
       
        if (group is null)
        {
            return new ()
            {
                Message = $"Service entity group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var serviceEntity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryServiceEntity>();
        serviceEntity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        serviceEntity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.AddAsync(serviceEntity,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service entity with Guid {serviceEntity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(serviceEntity.Guid)
            }
        };
    }
}