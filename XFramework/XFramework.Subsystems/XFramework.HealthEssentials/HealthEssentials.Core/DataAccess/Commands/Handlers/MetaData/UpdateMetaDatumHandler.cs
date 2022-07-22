using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class UpdateMetaDatumHandler : CommandBaseHandler, IRequestHandler<UpdateMetaDatumCmd, CmdResponse<UpdateMetaDatumCmd>>
{
    public UpdateMetaDatumHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateMetaDatumCmd>> Handle(UpdateMetaDatumCmd request, CancellationToken cancellationToken)
    {
        var existingMetaDatum = await _dataLayer.HealthEssentialsContext.MetaData.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMetaDatum is null)
        {
            return new ()
            {
                Message = $"Meta Datum with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedMetaDatum = request.Adapt(existingMetaDatum);

        if (request.EntityGuid is not null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.MetaDataEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Entity with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedMetaDatum.Entity = entity;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedMetaDatum);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Meta Datum with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}