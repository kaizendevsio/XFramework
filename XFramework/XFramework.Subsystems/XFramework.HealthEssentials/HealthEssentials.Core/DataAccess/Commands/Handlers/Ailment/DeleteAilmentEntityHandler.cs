using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentEntityHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentEntityCmd, CmdResponse<DeleteAilmentEntityCmd>>
{
    public DeleteAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentEntityCmd>> Handle(DeleteAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentEntity = await _dataLayer.HealthEssentialsContext.AilmentEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingAilmentEntity is null)
        {
            return new ()
            {
                Message = $"Ailment Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingAilmentEntity.IsDeleted = true;
        existingAilmentEntity.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingAilmentEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}