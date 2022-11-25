using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderCmd>>
{
    public UpdatePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderCmd>> Handle(UpdatePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
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
        var updatedConsultationJobOrder = request.Adapt(existingConsultationJobOrder);

        if (request.PharmacyJobOrderGuid is null)
        {
            var pharmacyJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyJobOrderGuid}", CancellationToken.None);
            if (pharmacyJobOrder is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Job Order with Guid {request.PharmacyJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedConsultationJobOrder.PharmacyJobOrder = pharmacyJobOrder;
        }

        if (request.ConsultationJobOrderGuid is null)
        {
            var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
            if (consultationJobOrder is null)
            {
                return new ()
                {
                    Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedConsultationJobOrder.ConsultationJobOrder = consultationJobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedConsultationJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation Job Order with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}