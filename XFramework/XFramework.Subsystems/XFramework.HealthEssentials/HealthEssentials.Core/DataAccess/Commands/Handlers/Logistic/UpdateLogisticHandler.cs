using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticCmd, CmdResponse<UpdateLogisticCmd>>
{
    public UpdateLogisticHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLogisticCmd>> Handle(UpdateLogisticCmd request, CancellationToken cancellationToken)
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
        
        var updateRecord = request.Adapt(existingRecord);
        updateRecord.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updateRecord.Status = (int) GenericStatusType.Pending;

        _dataLayer.HealthEssentialsContext.Logistics.Update(updateRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Logistic with Guid {updateRecord.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updateRecord.Guid)
            }
        };
    }
}