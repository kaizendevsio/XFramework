using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticRiderHandleCmd, CmdResponse<UpdateLogisticRiderHandleCmd>>
{
    public UpdateLogisticRiderHandleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateLogisticRiderHandleCmd>> Handle(UpdateLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        var existingLogisticRiderHandle = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingLogisticRiderHandle == null)
        {
            return new()
            {
                Message = $"Logistic rider handle with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var logistic = await _dataLayer.HealthEssentialsContext.Logistics
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.LogisticGuid}", cancellationToken: cancellationToken);
       
        if (logistic is null)
        {
            return new ()
            {
                Message = $"Logistic with Guid {request.LogisticGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.RiderGuid}", cancellationToken: cancellationToken);
       
        if (rider is null)
        {
            return new ()
            {
                Message = $"Logistic rider with Guid {request.RiderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = new LogisticRiderHandle()
        {
            Logistic = logistic,
            LogisticRider = rider,
            Guid = $"{Guid.NewGuid()}",
            Status = (short) GenericStatusType.Approved
        };

        var updatedLogisticRiderHandle = request.Adapt(existingLogisticRiderHandle);
        updatedLogisticRiderHandle.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updatedLogisticRiderHandle.Logistic = logistic;
        updatedLogisticRiderHandle.LogisticRider = rider;


        _dataLayer.HealthEssentialsContext.LogisticRiderHandles.Update(updatedLogisticRiderHandle);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Logistic rider handle with Guid {updatedLogisticRiderHandle.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedLogisticRiderHandle.Guid)
            }
        };
    }
}