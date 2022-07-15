using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticCmd, CmdResponse<DeleteLogisticCmd>>
{
    public DeleteLogisticHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteLogisticCmd>> Handle(DeleteLogisticCmd request, CancellationToken cancellationToken)
    {
        var existingLogistic = await _dataLayer.HealthEssentialsContext.Logistics.FirstOrDefaultAsync(x => x.Guid ==$"{request.Guid}", CancellationToken.None);
        if (existingLogistic == null)
        {
            return new()
            {
                Message = $"Logistic with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        existingLogistic.IsDeleted = true;
        existingLogistic.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLogistic);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}