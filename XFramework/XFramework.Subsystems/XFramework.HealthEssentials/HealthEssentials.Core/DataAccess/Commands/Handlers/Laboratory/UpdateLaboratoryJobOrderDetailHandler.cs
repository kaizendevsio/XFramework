using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryJobOrderDetailCmd, CmdResponse<UpdateLaboratoryJobOrderDetailCmd>>
{
    public UpdateLaboratoryJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryJobOrderDetailCmd>> Handle(UpdateLaboratoryJobOrderDetailCmd request, CancellationToken cancellationToken)
    {
        var existingDetail = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderDetails.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDetail is null)
        {
            return new ()
            {
                Message = $"Laboratory Job Order Detail with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDetail = request.Adapt(existingDetail);

        if (request.LaboratoryJobOrderGuid is null)
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
            updatedDetail.LaboratoryJobOrder = jobOrder;
        }

        if (request.LaboratoryServiceGuid is null)
        {
            var service = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryServiceGuid}", CancellationToken.None);
            if (service is null)
            {
                return new ()
                {
                    Message = $"Laboratory Service with Guid {request.LaboratoryServiceGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDetail.LaboratoryService = service;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Job Order Detail with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}