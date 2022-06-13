using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticCmd, CmdResponse<DeleteLogisticCmd>>
{
    public DeleteLogisticHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLogisticCmd>> Handle(DeleteLogisticCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.Logistics
            .FirstOrDefaultAsync(x => x.Guid ==$"{request.Guid}", CancellationToken.None);

        if (existingRecord == null)
        {
            return new()
            {
                Message = $"Logistic with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Logistic with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}