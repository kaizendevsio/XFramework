using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationJobOrderMedicineCmd, CmdResponse<DeleteConsultationJobOrderMedicineCmd>>
{
    public DeleteConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteConsultationJobOrderMedicineCmd>> Handle(DeleteConsultationJobOrderMedicineCmd request, CancellationToken cancellationToken)
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
        
        existingJobOrderMedicine.IsDeleted = true;
        existingJobOrderMedicine.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrderMedicine);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation Job Order Medicine with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}