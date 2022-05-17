using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class AddSupportedConsultationHandler : CommandBaseHandler, IRequestHandler<AddSupportedConsultationCmd, CmdResponse<AddSupportedConsultationCmd>>
{
    public AddSupportedConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<AddSupportedConsultationCmd>> Handle(AddSupportedConsultationCmd request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.ConsultationGuid}", cancellationToken: cancellationToken);
       
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.DoctorGuid}", cancellationToken: cancellationToken);
       
        if (doctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.DoctorGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<DoctorConsultation>();
        entity.DoctorId = doctor.Id;
        entity.ConsultationId = consultation.Id;
        
        await _dataLayer.HealthEssentialsContext.DoctorConsultations.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}