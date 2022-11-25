using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderMedicineCmd, CmdResponse<CreatePharmacyJobOrderMedicineCmd>>
{
    public CreatePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderMedicineCmd>> Handle(CreatePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
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
        
        var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
        if (medicine is null)
        {
            return new ()
            {
                Message = $"Medicine with Guid {request.MedicineGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var medicineIntake = await _dataLayer.HealthEssentialsContext.MedicineIntakes.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineIntakeGuid}", CancellationToken.None);
        if (medicineIntake is null)
        {
            return new ()
            {
                Message = $"Medicine Intake with Guid {request.MedicineIntakeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrderMedicine = request.Adapt<PharmacyJobOrderMedicine>();
        jobOrderMedicine.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrderMedicine.PharmacyJobOrder = jobOrder;
        jobOrderMedicine.Medicine = medicine;
        jobOrderMedicine.MedicineIntake = medicineIntake;
      
        await _dataLayer.HealthEssentialsContext.PharmacyJobOrderMedicines.AddAsync(jobOrderMedicine, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrderMedicine.Guid);
        return new()
        {
            Message = $"Pharmacy Job Order Medicine with Guid {jobOrderMedicine.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}