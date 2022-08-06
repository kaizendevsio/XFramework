using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class DeleteMetaDataEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMetaDataEntityCmd, CmdResponse<DeleteMetaDataEntityCmd>>
{
    public DeleteMetaDataEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteMetaDataEntityCmd>> Handle(DeleteMetaDataEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.MetaDataEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Entity with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}