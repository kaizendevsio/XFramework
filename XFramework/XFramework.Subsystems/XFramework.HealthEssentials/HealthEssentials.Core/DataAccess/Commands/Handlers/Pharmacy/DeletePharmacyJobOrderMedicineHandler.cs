using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderMedicineCmd, CmdResponse<DeletePharmacyJobOrderMedicineCmd>>
{
    public DeletePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderMedicineCmd>> Handle(DeletePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrderMedicine = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderMedicines.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrderMedicine is null)
        {
            return new ()
            {
                Message = $"Pharmacy Job Order Medicine with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingJobOrderMedicine.IsDeleted = true;
        existingJobOrderMedicine.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrderMedicine);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Job Order Medicine with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}