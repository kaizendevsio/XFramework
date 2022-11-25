using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientAilmentHandler : CommandBaseHandler, IRequestHandler<UpdatePatientAilmentCmd, CmdResponse<UpdatePatientAilmentCmd>>
{
    public UpdatePatientAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePatientAilmentCmd>> Handle(UpdatePatientAilmentCmd request, CancellationToken cancellationToken)
    {
        var existingPatientAilment = await _dataLayer.HealthEssentialsContext.PatientAilments.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatientAilment is null)
        {
            return new ()
            {
                Message = $"PatientAilment with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedPatientAilment = request.Adapt(existingPatientAilment);

        if (request.PatientGuid is null)
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
            updatedPatientAilment.Patient = patient;
        }

        if (request.AilmentGuid is null)
        {
            var ailment = await _dataLayer.HealthEssentialsContext.Ailments.FirstOrDefaultAsync(x => x.Guid == $"{request.AilmentGuid}", CancellationToken.None);
            if (ailment is null)
            {
                return new ()
                {
                    Message = $"Ailment with Guid {request.AilmentGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPatientAilment.Ailment = ailment;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedPatientAilment);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"PatientAilment with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };  
    }
}