using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticHandler : CommandBaseHandler, IRequestHandler<CreateLogisticCmd, CmdResponse<CreateLogisticCmd>>
{
    public CreateLogisticHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticCmd>> Handle(CreateLogisticCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Logistic>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        _dataLayer.HealthEssentialsContext.Logistics.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Logistic with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}