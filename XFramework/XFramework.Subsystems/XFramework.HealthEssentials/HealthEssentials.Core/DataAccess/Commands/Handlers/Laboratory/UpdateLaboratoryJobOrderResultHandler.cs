using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryJobOrderResultHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryJobOrderResultCmd, CmdResponse<UpdateLaboratoryJobOrderResultCmd>>
{
    public UpdateLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryJobOrderResultCmd>> Handle(UpdateLaboratoryJobOrderResultCmd request, CancellationToken cancellationToken)
    {
       var existingResult = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
       if (existingResult is null)
       {
           return new()
           {
               Message = $"Laboratory Job Order Result with Guid {request.Guid} does not exist",
               HttpStatusCode = HttpStatusCode.NotFound
           };
       }
       var updatedResult = request.Adapt(existingResult);

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
           updatedResult.LaboratoryJobOrder = jobOrder;
       }

       _dataLayer.HealthEssentialsContext.Update(updatedResult);
       await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
         
            return new()
            {
                Message = $"Laboratory Job Order Result with Guid {request.Guid} updated successfully",
                HttpStatusCode = HttpStatusCode.Accepted
            };

    }
}