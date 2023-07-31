using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<CreateLogisticRiderHandleCmd, CmdResponse<CreateLogisticRiderHandleCmd>>
{
    public CreateLogisticRiderHandleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticRiderHandleCmd>> Handle(CreateLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        var logistic = await _dataLayer.HealthEssentialsContext.Logistics.FirstOrDefaultAsync(i => i.Guid == $"{request.LogisticGuid}", cancellationToken: cancellationToken);
        if (logistic is null)
        {
            return new ()
            {
                Message = $"Logistic with Guid {request.LogisticGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders.FirstOrDefaultAsync(i => i.Guid == $"{request.RiderGuid}", cancellationToken: cancellationToken);
        if (rider is null)
        {
            return new ()
            {
                Message = $"Logistic rider with Guid {request.RiderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var handle = request.Adapt<LogisticRiderHandle>();
        handle.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        handle.Logistic = logistic;
        handle.LogisticRider = rider;
        
        await _dataLayer.HealthEssentialsContext.LogisticRiderHandles.AddAsync(handle,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(handle.Guid);
        return new()
        {
            Message = $"Logistic rider handle with Guid {handle.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}