using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateAilmentEntityGroupCmd, CmdResponse<CreateAilmentEntityGroupCmd>>
{
    public CreateAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateAilmentEntityGroupCmd>> Handle(CreateAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var ailmentEntityGroup = request.Adapt<AilmentTypeGroup>();
        ailmentEntityGroup.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.AilmentEntityGroups.AddAsync(ailmentEntityGroup, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(ailmentEntityGroup.Guid);
        return new()
        {
            Message = $"Ailment Entity Group with Guid {ailmentEntityGroup.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}