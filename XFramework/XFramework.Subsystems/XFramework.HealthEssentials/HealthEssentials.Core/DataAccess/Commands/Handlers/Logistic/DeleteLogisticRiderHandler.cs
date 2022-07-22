using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderCmd, CmdResponse<DeleteLogisticRiderCmd>>
{
    public DeleteLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteLogisticRiderCmd>> Handle(DeleteLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        var existingLogisticRider = await _dataLayer.HealthEssentialsContext.LogisticRiders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLogisticRider == null)
        {
            return new()
            {
                Message = $"Logistic rider with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLogisticRider.IsDeleted = true;
        existingLogisticRider.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLogisticRider);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic rider with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}