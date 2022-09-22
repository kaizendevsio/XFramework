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
        var updatedLaboratoryMember = request.Adapt(existingLaboratoryMember);

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
            updatedLaboratoryMember.CredentialGuid = credential.Guid;
        }

        if (request.LaboratoryGuid is not null)
        {
            var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(i => i.Guid == $"{request.LaboratoryGuid}", cancellationToken: cancellationToken);
            if (laboratory is null)
            {
                return new ()
                {
                    Message = $"Laboratory with Guid {request.LaboratoryGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryMember.Laboratory = laboratory;
        }

        if (request.LaboratoryLocationGuid is not null)
        {
            var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryLocationGuid}", CancellationToken.None);
            if (laboratoryLocation is null)
            {
                return new ()
                {
                    Message = $"Laboratory Location with Guid {request.LaboratoryLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratoryMember.LaboratoryLocation = laboratoryLocation;
        }
        
        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryMember);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory member with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}