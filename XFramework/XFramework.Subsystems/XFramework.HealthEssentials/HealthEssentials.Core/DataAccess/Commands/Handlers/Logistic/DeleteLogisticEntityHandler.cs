using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticEntityHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticEntityCmd, CmdResponse<DeleteLogisticEntityCmd>>
{
    public DeleteLogisticEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLogisticEntityCmd>> Handle(DeleteLogisticEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.LogisticEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity == null)
        {
            return new()
            {
                Message = $"Logistic Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}