using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<DeletePharmacyJobOrderConsultationJobOrderCmd>>
{
    public DeletePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderConsultationJobOrderCmd>> Handle(DeletePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingConsultationJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingConsultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingConsultationJobOrder.IsDeleted = true;
        existingConsultationJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingConsultationJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}