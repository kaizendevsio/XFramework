using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryEntityHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryEntityCmd, CmdResponse<CreateLaboratoryEntityCmd>>
{
    public CreateLaboratoryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryEntityCmd>> Handle(CreateLaboratoryEntityCmd request, CancellationToken cancellationToken)
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

        var entity = request.Adapt<LaboratoryType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Laboratory Entity with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}