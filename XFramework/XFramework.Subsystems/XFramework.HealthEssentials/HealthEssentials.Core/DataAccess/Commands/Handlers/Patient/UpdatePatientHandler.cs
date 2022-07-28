using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientHandler : CommandBaseHandler, IRequestHandler<UpdatePatientCmd, CmdResponse<UpdatePatientCmd>>
{
    public UpdatePatientHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdatePatientCmd>> Handle(UpdatePatientCmd request, CancellationToken cancellationToken)
    {
        var existingPatient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedPatient = request.Adapt(existingPatient);

        if (request.CredentialGuid is null)
        {
            var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
            if (credential is null)
            {
                return new ()
                {
                    Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPatient.CredentialId = credential.Guid;
        }

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.PatientEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Entity with Guid {request.Guid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPatient.Entity = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedPatient);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Patient with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}