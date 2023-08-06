using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts;

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
        
        var dosageUnit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.DosageUnitGuid}", CancellationToken.None);
        if (dosageUnit is null)
        {
            return new ()
            {
                Message = $"Dosage Unit with Guid {request.DosageUnitGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var durationUnit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.DurationUnitGuid}", CancellationToken.None);
        if (durationUnit is null)
        {
            return new ()
            {
                Message = $"Duration Unit with Guid {request.DurationUnitGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var intakeUnit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.IntakeUnitGuid}", CancellationToken.None);
        if (intakeUnit is null)
        {
            return new ()
            {
                Message = $"Intake Unit with Guid {request.IntakeUnitGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        /*var medicineIntake = await _dataLayer.HealthEssentialsContext.MedicineIntakes.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineIntakeGuid}", CancellationToken.None);
        if (medicineIntake is null)
        {
            return new ()
            {
                Message = $"Medicine Intake with Guid {request.MedicineIntakeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }*/

        var jobOrderMedicine = request.Adapt<PharmacyJobOrderMedicine>();
        jobOrderMedicine.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrderMedicine.PharmacyJobOrder = jobOrder;
        jobOrderMedicine.Medicine = medicine;
        //jobOrderMedicine.MedicineIntake = medicineIntake;
        jobOrderMedicine.DosageUnit = dosageUnit;
        jobOrderMedicine.DurationUnit = durationUnit;
        jobOrderMedicine.IntakeUnit = intakeUnit;
      
        await _dataLayer.HealthEssentialsContext.PharmacyJobOrderMedicines.AddAsync(jobOrderMedicine, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrderMedicine.Guid);
        return new()
        {
            Message = $"Pharmacy Job Order Medicine with Guid {jobOrderMedicine.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}