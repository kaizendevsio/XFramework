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
        var type = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", cancellationToken: cancellationToken);
       
        if (type is null)
        {
            return new ()
            {
                Message = $"Service type group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryServiceEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.GroupId = type.Id;
        
        _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service type with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}