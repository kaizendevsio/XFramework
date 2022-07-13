using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class CreateMetaDatumHandler : CommandBaseHandler, IRequestHandler<CreateMetaDatumCmd, CmdResponse<CreateMetaDatumCmd>>
{
    public CreateMetaDatumHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateMetaDatumCmd>> Handle(CreateMetaDatumCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.MetaDataEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        
        if (entity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var metaDatum = request.Adapt<MetaDatum>();
        metaDatum.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        metaDatum.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.MetaData.AddAsync(metaDatum, cancellationToken);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        request.Guid = Guid.Parse(metaDatum.Guid);
        return new()
        {
            Message = $"MetaDatum with Guid {metaDatum.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}