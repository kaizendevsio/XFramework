using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class CreateMetaDataEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateMetaDataEntityGroupCmd, CmdResponse<CreateMetaDataEntityGroupCmd>>
{
    public CreateMetaDataEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateMetaDataEntityGroupCmd>> Handle(CreateMetaDataEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<MetaDataTypeGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.MetaDataEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Meta Data Entity Group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}