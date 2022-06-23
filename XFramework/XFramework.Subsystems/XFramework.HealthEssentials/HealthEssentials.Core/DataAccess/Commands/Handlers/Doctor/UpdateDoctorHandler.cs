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
        var existingDoctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingDoctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var entity = await _dataLayer.HealthEssentialsContext.DoctorEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", cancellationToken: cancellationToken);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDoctor = request.Adapt(existingDoctor);
        existingDoctor.CredentialId = credential.Id;
        existingDoctor.Entity = entity;
        
        _dataLayer.HealthEssentialsContext.Update(existingDoctor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Doctor Identity Updated Successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}