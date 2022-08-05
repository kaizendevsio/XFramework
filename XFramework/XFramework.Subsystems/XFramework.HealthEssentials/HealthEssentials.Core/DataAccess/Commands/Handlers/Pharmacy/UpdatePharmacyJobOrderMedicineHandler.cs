using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderMedicineCmd, CmdResponse<UpdatePharmacyJobOrderMedicineCmd>>
{
    public UpdatePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderMedicineCmd>> Handle(UpdatePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
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
        var updatedJobOrderMedicine = request.Adapt(existingJobOrderMedicine);

        if (request.PharmacyJobOrderGuid is null)
        {
            var jobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyJobOrderGuid}", CancellationToken.None);
            if (jobOrder is null)
            {
                return new ()
                {
                    Message = $"Pharmacy Job Order with Guid {request.PharmacyJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrderMedicine.PharmacyJobOrder = jobOrder;
        }

        if (request.MedicineGuid is null)
        {
            var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
            if (medicine is null)
            {
                return new ()
                {
                    Message = $"Medicine with Guid {request.MedicineGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrderMedicine.Medicine = medicine;
        }

        if (request.MedicineIntakeGuid is null)
        {
            var medicineIntake = await _dataLayer.HealthEssentialsContext.MedicineIntakes.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineIntakeGuid}", CancellationToken.None);
            if (medicineIntake is null)
            {
                return new ()
                {
                    Message = $"Medicine Intake with Guid {request.MedicineIntakeGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrderMedicine.MedicineIntake = medicineIntake;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedJobOrderMedicine);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Job Order Medicine with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}