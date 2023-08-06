using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class CreateTagEntityHandler : CommandBaseHandler, IRequestHandler<CreateTagEntityCmd, CmdResponse<CreateTagEntityCmd>>
{
    public CreateTagEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateTagEntityCmd>> Handle(CreateTagEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.TagEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Tag group with guid {request.GroupGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<TagType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.TagEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Tag entity with guid {entity.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}