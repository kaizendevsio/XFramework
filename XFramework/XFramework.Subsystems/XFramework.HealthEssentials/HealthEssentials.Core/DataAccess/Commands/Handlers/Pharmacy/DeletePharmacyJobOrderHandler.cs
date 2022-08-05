using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderCmd, CmdResponse<DeletePharmacyJobOrderCmd>>
{
    public DeletePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderCmd>> Handle(DeletePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrder is null)
        {
            return new ()
            {
                Message = $"Pharmacy Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingJobOrder.IsDeleted = true;
        existingJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}