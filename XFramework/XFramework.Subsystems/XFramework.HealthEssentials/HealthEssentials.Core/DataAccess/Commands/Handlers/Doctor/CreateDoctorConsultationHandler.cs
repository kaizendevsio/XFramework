using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorConsultationHandler : CommandBaseHandler, IRequestHandler<CreateDoctorConsultationCmd, CmdResponse<CreateDoctorConsultationCmd>>
{
    public CreateDoctorConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateDoctorConsultationCmd>> Handle(CreateDoctorConsultationCmd request, CancellationToken cancellationToken)
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
        
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(x => x.Guid == $"{request.DoctorGuid}", CancellationToken.None);
        if (doctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.DoctorGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var doctorConsultation = request.Adapt<DoctorConsultation>();
        doctorConsultation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        doctorConsultation.Doctor = doctor;
        doctorConsultation.Consultation = consultation;
        
        await _dataLayer.HealthEssentialsContext.DoctorConsultations.AddAsync(doctorConsultation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(doctorConsultation.Guid);
        return new()
        {
            Message = $"Doctor Consultation with Guid {doctorConsultation.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}