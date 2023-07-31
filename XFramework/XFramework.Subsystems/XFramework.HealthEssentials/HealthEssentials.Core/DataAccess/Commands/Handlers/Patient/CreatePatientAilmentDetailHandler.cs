using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientAilmentDetailHandler : CommandBaseHandler, IRequestHandler<CreatePatientAilmentDetailCmd, CmdResponse<CreatePatientAilmentDetailCmd>>
{
    public CreatePatientAilmentDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientAilmentDetailCmd>> Handle(CreatePatientAilmentDetailCmd request, CancellationToken cancellationToken)
    {
        var patientAilment = await _dataLayer.HealthEssentialsContext.PatientAilments.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientAilmentGuid}", CancellationToken.None);
        if (patientAilment is null)
        {
            return new ()
            {
                Message = $"Patient Ailment with Guid {request.PatientAilmentGuid} does not exist",
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

        var detail = request.Adapt<PatientAilmentDetail>();
        detail.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        detail.PatientAilment = patientAilment;
        detail.ConsultationJobOrder = consultationJobOrder;
        
        await _dataLayer.HealthEssentialsContext.PatientAilmentDetails.AddAsync(detail, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(detail.Guid);
        return new()
        {
            Message = $"Patient Ailment Detail with Guid {detail.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}