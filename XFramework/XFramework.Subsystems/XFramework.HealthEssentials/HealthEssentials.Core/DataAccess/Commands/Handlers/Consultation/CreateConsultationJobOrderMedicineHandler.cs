using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderMedicineCmd, CmdResponse<CreateConsultationJobOrderMedicineCmd>>
{
    public CreateConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderMedicineCmd>> Handle(CreateConsultationJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (jobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var consultationJobOrderMedicine = request.Adapt<ConsultationJobOrderMedicine>();

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

        if (request.MedicineIntakeGuid is not null)
        {
            var medicineIntake = await _dataLayer.HealthEssentialsContext.MedicineIntakes.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineIntakeGuid}", CancellationToken.None);
            if (medicineIntake is null)
            {
                return new()
                {
                    Message = $"Medicine Intake with Guid {request.MedicineIntakeGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            consultationJobOrderMedicine.MedicineIntake = medicineIntake;
        }

        consultationJobOrderMedicine.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultationJobOrderMedicine.ConsultationJobOrder = jobOrder;
        consultationJobOrderMedicine.Medicine = medicine;
        consultationJobOrderMedicine.DosageUnit = dosageUnit;
        consultationJobOrderMedicine.DurationUnit = durationUnit;
        consultationJobOrderMedicine.IntakeUnit = intakeUnit;
        
        await _dataLayer.HealthEssentialsContext.ConsultationJobOrderMedicines.AddAsync(consultationJobOrderMedicine, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(consultationJobOrderMedicine.Guid);
        return new ()
        {
            Message = $"Consultation Job Order Medicine with Guid {consultationJobOrderMedicine.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}