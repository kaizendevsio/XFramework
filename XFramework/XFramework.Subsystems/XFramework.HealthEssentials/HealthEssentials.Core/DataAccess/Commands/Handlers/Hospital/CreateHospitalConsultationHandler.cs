using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<CreateHospitalConsultationCmd, CmdResponse<CreateHospitalConsultationCmd>>
{
    public CreateHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalConsultationCmd>> Handle(CreateHospitalConsultationCmd request, CancellationToken cancellationToken)
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
        
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.ConsultationGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var hospitalConsultation = request.Adapt<HospitalConsultation>();
        hospitalConsultation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        hospitalConsultation.Hospital = hospital;
        hospitalConsultation.Consultation = consultation;
        
        await _dataLayer.HealthEssentialsContext.HospitalConsultations.AddAsync(hospitalConsultation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(hospitalConsultation.Guid);
        return new()
        {
            Message = $"Hospital Consultation with Guid {hospitalConsultation.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}