using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticHandler : CommandBaseHandler, IRequestHandler<CreateLogisticCmd, CmdResponse<CreateLogisticCmd>>
{
    public CreateLogisticHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticCmd>> Handle(CreateLogisticCmd request, CancellationToken cancellationToken)
    {
        var logistic = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Logistic>();
        logistic.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        logistic.Status = (int) GenericStatusType.Pending;

        await _dataLayer.HealthEssentialsContext.Logistics.AddAsync(logistic,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(logistic.Guid);
        return new()
        {
            Message = $"Logistic with Guid {logistic.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}