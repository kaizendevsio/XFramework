using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorConsultationJobOrderCmd, CmdResponse<DeleteDoctorConsultationJobOrderCmd>>
{
    public DeleteDoctorConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteDoctorConsultationJobOrderCmd>> Handle(DeleteDoctorConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingDoctorConsultationJobOrder = await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctorConsultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Doctor Consultation Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDoctorConsultationJobOrder.IsDeleted = true;
        existingDoctorConsultationJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDoctorConsultationJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Consultation Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}