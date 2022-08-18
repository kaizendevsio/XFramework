using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalConsultationCmd, CmdResponse<UpdateHospitalConsultationCmd>>
{
    public UpdateHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalConsultationCmd>> Handle(UpdateHospitalConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalConsultation = await _dataLayer.HealthEssentialsContext.HospitalConsultations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalConsultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedHospitalConsultation = request.Adapt(existingHospitalConsultation);

        if (request.HospitalGuid is null)
        {
            var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
            if (hospital is null)
            {
                return new ()
                {
                    Message = $"Hospital with Guid {request.HospitalGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalConsultation.Hospital = hospital;
        }

        if (request.ConsultationGuid is null)
        {
            var consultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
            if (consultation is null)
            {
                return new ()
                {
                    Message = $"Consultation with Guid {request.ConsultationGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalConsultation.Consultation = consultation;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedHospitalConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}