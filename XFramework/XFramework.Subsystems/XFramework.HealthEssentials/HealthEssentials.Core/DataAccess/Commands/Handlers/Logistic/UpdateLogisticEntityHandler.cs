using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticEntityHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticEntityCmd, CmdResponse<UpdateLogisticEntityCmd>>
{
    public UpdateLogisticEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLogisticEntityCmd>> Handle(UpdateLogisticEntityCmd request, CancellationToken cancellationToken)
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
        var updatedEntity = request.Adapt(existingEntity);

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}