using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class DeleteTagEntityHandler : CommandBaseHandler, IRequestHandler<DeleteTagEntityCmd, CmdResponse<DeleteTagEntityCmd>>
{
    public DeleteTagEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteTagEntityCmd>> Handle(DeleteTagEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.TagEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity == null)
        {
            return new()
            {
                Message = $"Tag Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Tag Entity with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}