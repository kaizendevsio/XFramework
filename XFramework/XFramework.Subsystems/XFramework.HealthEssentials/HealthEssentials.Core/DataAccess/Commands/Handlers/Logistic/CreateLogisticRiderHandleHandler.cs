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
        
        _dataLayer.HealthEssentialsContext.LogisticRiderHandles.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Logistic rider handle with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}