using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryJobOrderDetailCmd, CmdResponse<CreateLaboratoryJobOrderDetailCmd>>
{
    public CreateLaboratoryJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryJobOrderDetailCmd>> Handle(CreateLaboratoryJobOrderDetailCmd request, CancellationToken cancellationToken)
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
        
        var service = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryServiceGuid}", CancellationToken.None);
        if (service is null)
        {
            return new ()
            {
                Message = $"Laboratory Service with Guid {request.LaboratoryServiceGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrderDetail = request.Adapt<LaboratoryJobOrderDetail>();
        jobOrderDetail.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrderDetail.LaboratoryJobOrder = jobOrder;
        jobOrderDetail.LaboratoryService = service;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderDetails.AddAsync(jobOrderDetail, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrderDetail.Guid);
        return new()
        {
            Message = $"Laboratory Job Order Detail with Guid {jobOrderDetail.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}