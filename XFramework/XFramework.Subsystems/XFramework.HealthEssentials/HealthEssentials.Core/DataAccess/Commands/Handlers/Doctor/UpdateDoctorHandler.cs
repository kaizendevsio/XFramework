using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorCmd, CmdResponse<UpdateDoctorCmd>>
{
    public UpdateDoctorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateDoctorCmd>> Handle(UpdateDoctorCmd request, CancellationToken cancellationToken)
    {
        var existingDoctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDoctor = request.Adapt(existingDoctor);

        if (request.CredentialGuid is not null)
        {
            var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", CancellationToken.None);
            if (credential is null)
            {
                return new ()
                {
                    Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctor.CredentialGuid = credential.Guid;
        }

        if (request.EntityGuid is not null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.DoctorEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Doctor entity with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctor.Type = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDoctor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}