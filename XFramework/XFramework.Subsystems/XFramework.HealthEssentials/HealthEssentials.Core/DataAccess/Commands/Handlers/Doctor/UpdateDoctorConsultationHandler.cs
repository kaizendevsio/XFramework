using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorConsultationHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorConsultationCmd, CmdResponse<UpdateDoctorConsultationCmd>>
{
    public UpdateDoctorConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateDoctorConsultationCmd>> Handle(UpdateDoctorConsultationCmd request, CancellationToken cancellationToken)
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
        var updatedDoctorConsultation = request.Adapt(existingDoctorConsultation);

        if (request.ConsultationGuid is null)
        {
            var consultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
            if (consultation is null)
            {
                return new ()
                {
                    Message = $"Consultation with Guid {request.ConsultationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorConsultation.Consultation = consultation;
        }

        if (request.DoctorGuid is null)
        {
            var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(x => x.Guid == $"{request.DoctorGuid}", CancellationToken.None);
            if (doctor is null)
            {
                return new ()
                {
                    Message = $"Doctor with Guid {request.DoctorGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorConsultation.Doctor = doctor;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDoctorConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Consultation with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}