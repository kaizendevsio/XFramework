using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientConsultationHandler : CommandBaseHandler, IRequestHandler<CreatePatientConsultationCmd, CmdResponse<CreatePatientConsultationCmd>>
{
    public CreatePatientConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientConsultationCmd>> Handle(CreatePatientConsultationCmd request, CancellationToken cancellationToken)
    {
        var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
        if (patient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.PatientGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (consultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patientConsultation = request.Adapt<PatientConsultation>();
        patientConsultation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        patientConsultation.Patient = patient;
        patientConsultation.ConsultationJobOrder = consultationJobOrder;
        
        await _dataLayer.HealthEssentialsContext.PatientConsultations.AddAsync(patientConsultation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(patientConsultation.Guid);
        return new()
        {
            Message = $"Patient Consultation with Guid {patientConsultation.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}