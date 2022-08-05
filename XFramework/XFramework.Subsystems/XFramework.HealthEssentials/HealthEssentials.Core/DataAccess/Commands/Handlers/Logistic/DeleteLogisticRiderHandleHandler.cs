using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderHandleCmd, CmdResponse<DeleteLogisticRiderHandleCmd>>
{
    public DeleteLogisticRiderHandleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteLogisticRiderHandleCmd>> Handle(DeleteLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        var existingLogisticRiderHandle = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLogisticRiderHandle == null)
        {
            return new()
            {
                Message = $"Logistic rider handle with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLogisticRiderHandle.IsDeleted = true;
        existingLogisticRiderHandle.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLogisticRiderHandle);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic rider handle with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}