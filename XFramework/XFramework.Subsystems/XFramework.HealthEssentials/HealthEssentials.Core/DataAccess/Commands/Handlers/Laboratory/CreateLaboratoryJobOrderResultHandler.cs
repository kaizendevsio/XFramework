using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryJobOrderResultHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryJobOrderResultCmd, CmdResponse<CreateLaboratoryJobOrderResultCmd>>
{
    public CreateLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryJobOrderResultCmd>> Handle(CreateLaboratoryJobOrderResultCmd request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryJobOrderGuid}", CancellationToken.None);
        if (jobOrder is null)
        {
            return new ()
            {
                Message = $"Laboratory Job Order with Guid {request.LaboratoryJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var result = request.Adapt<LaboratoryJobOrderResult>();
        result.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        result.LaboratoryJobOrder = jobOrder;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults.AddAsync(result, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(result.Guid);
        return new()
        {
            Message = $"Laboratory Job Order Result with Guid {result.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}