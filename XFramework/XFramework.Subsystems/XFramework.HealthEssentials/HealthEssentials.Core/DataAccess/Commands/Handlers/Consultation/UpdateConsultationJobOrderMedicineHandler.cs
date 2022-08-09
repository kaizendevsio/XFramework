using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationJobOrderMedicineCmd, CmdResponse<UpdateConsultationJobOrderMedicineCmd>>
{
    public UpdateConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationJobOrderMedicineCmd>> Handle(UpdateConsultationJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrderMedicine = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderMedicines.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrderMedicine is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order Medicine with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedJobOrderMedicine = request.Adapt(existingJobOrderMedicine);

        if (request.ConsultationJobOrderGuid is not null)
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
            updatedJobOrderMedicine.ConsultationJobOrder = jobOrder;
        }

        if (request.MedicineGuid is not null)
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

        if (request.MedicineIntakeGuid is not null)
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
        
        return new()
        {
            Message = $"Consultation Job Order Medicine with Guid {updatedJobOrderMedicine.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}