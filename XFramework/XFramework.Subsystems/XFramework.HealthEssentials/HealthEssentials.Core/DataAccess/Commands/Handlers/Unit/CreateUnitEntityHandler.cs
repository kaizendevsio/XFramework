using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class CreateUnitEntityHandler : CommandBaseHandler, IRequestHandler<CreateUnitEntityCmd, CmdResponse<CreateUnitEntityCmd>>
{
    public CreateUnitEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateUnitEntityCmd>> Handle(CreateUnitEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.UnitEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Group with guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<UnitType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;

        await _dataLayer.HealthEssentialsContext.UnitEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Unit entity with guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        }; 
    }
}