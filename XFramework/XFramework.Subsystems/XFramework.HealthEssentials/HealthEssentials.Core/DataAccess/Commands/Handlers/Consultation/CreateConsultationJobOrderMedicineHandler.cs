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

        var consultationJobOrderMedicine = request.Adapt<ConsultationJobOrderMedicine>();
        consultationJobOrderMedicine.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultationJobOrderMedicine.ConsultationJobOrder = jobOrder;
        consultationJobOrderMedicine.Medicine = medicine;
        consultationJobOrderMedicine.MedicineIntake = medicineIntake;
        
        await _dataLayer.HealthEssentialsContext.ConsultationJobOrderMedicines.AddAsync(consultationJobOrderMedicine, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(consultationJobOrderMedicine.Guid);
        return new ()
        {
            Message = $"Consultation Job Order Medicine with Guid {consultationJobOrderMedicine.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}