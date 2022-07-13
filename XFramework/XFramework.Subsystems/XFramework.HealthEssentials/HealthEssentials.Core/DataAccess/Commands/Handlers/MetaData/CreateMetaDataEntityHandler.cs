using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class CreateMetaDataEntityHandler : CommandBaseHandler, IRequestHandler<CreateMetaDataEntityCmd, CmdResponse<CreateMetaDataEntityCmd>>
{
    public CreateMetaDataEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateMetaDataEntityCmd>> Handle(CreateMetaDataEntityCmd request, CancellationToken cancellationToken)
    {
        var entityGroup = await _dataLayer.HealthEssentialsContext.MetaDataEntityGroups
            .FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        
        if (entityGroup is null)
        {
            return new ()
            {
                Message = $"Entity group with guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<MetaDataEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = entityGroup;
        
        await _dataLayer.HealthEssentialsContext.MetaDataEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Entity {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };

    }
}