using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<CreatePharmacyJobOrderConsultationJobOrderCmd>>
{
    public CreatePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderConsultationJobOrderCmd>> Handle(CreatePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
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
        
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (consultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrder = request.Adapt<PharmacyJobOrderConsultationJobOrder>();
        jobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrder.PharmacyJobOrder = pharmacyJobOrder;
        jobOrder.ConsultationJobOrder = consultationJobOrder;
        
        await _dataLayer.HealthEssentialsContext.PharmacyJobOrderConsultationJobOrders.AddAsync(jobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrder.Guid);
        return new()
        {
            Message = $"Pharmacy Job Order Consultation Job Order with Guid {jobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}