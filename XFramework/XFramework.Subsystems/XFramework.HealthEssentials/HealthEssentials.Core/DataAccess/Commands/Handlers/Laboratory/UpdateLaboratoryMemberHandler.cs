using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryMemberCmd, CmdResponse<UpdateLaboratoryMemberCmd>>
{
    public UpdateLaboratoryMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryMemberCmd>> Handle(UpdateLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryMember = await _dataLayer.HealthEssentialsContext.LaboratoryMembers.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingLaboratoryMember is null)
        {
            return new ()
            {
                Message = $"Laboratory member with Guid {request.Guid} not found",
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
        
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(i => i.Guid == $"{request.LaboratoryGuid}", cancellationToken: cancellationToken);
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.LaboratoryGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (laboratoryLocation is null)
        {
            return new ()
            {
                Message = $"Laboratory Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var updatedLaboratoryMember = request.Adapt(existingLaboratoryMember);
        updatedLaboratoryMember.CredentialId = credential.Id;
        updatedLaboratoryMember.Laboratory = laboratory;
        updatedLaboratoryMember.LaboratoryLocation = laboratoryLocation;
        
        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Laboratory updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}