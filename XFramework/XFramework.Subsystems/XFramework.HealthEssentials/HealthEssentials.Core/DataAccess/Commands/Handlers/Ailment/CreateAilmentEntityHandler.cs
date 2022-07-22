using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentEntityHandler : CommandBaseHandler, IRequestHandler<CreateAilmentEntityCmd, CmdResponse<CreateAilmentEntityCmd>>
{
    public CreateAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentEntityCmd>> Handle(CreateAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        var ailmentEntityGroup = await _dataLayer.HealthEssentialsContext.AilmentEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (ailmentEntityGroup == null)
        {
            return new ()
            {
                Message = $"Ailment entity group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<AilmentEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = ailmentEntityGroup;
        
        await _dataLayer.HealthEssentialsContext.AilmentEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Ailment entity with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}