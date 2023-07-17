using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientAilmentHandler : CommandBaseHandler, IRequestHandler<CreatePatientAilmentCmd, CmdResponse<CreatePatientAilmentCmd>>
{
    public CreatePatientAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientAilmentCmd>> Handle(CreatePatientAilmentCmd request, CancellationToken cancellationToken)
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
        
        var ailment = await _dataLayer.HealthEssentialsContext.Ailments.FirstOrDefaultAsync(x => x.Guid == $"{request.AilmentGuid}", CancellationToken.None);
        if (ailment is null)
        {
            return new ()
            {
                Message = $"Ailment with Guid {request.AilmentGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patientAilment = request.Adapt<PatientAilment>();
        patientAilment.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        patientAilment.Patient = patient;
        patientAilment.Ailment = ailment;
        
        await _dataLayer.HealthEssentialsContext.PatientAilments.AddAsync(patientAilment, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(patientAilment.Guid);
        return new()
        {
            Message = $"Patient Ailment with Guid {patientAilment.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}