using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorConsultationHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorConsultationCmd, CmdResponse<DeleteDoctorConsultationCmd>>
{
    public DeleteDoctorConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteDoctorConsultationCmd>> Handle(DeleteDoctorConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingDoctorConsultation = await _dataLayer.HealthEssentialsContext.DoctorConsultations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctorConsultation is null)
        {
            return new ()
            {
                Message = $"Doctor Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDoctorConsultation.IsDeleted = true;
        existingDoctorConsultation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDoctorConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Consultation with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}