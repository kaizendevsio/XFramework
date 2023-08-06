using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class CreateUnitEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateUnitEntityGroupCmd, CmdResponse<CreateUnitEntityGroupCmd>>
{
    public CreateUnitEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateUnitEntityGroupCmd>> Handle(CreateUnitEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<UnitTypeGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.UnitEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Unit Entity Group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        }; 
    }
}