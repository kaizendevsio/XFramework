using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryMemberCmd, CmdResponse<CreateLaboratoryMemberCmd>>
{
    public CreateLaboratoryMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryMemberCmd>> Handle(CreateLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.LaboratoryGuid}", cancellationToken: cancellationToken);
       
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.LaboratoryGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
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

        var laboratoryMember = request.Adapt<LaboratoryMember>();
        laboratoryMember.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        laboratoryMember.Laboratory = laboratory;
        laboratoryMember.CredentialId = credential.Id;
        laboratoryMember.LaboratoryLocation = laboratoryLocation;
        laboratoryMember.Status = (int) (request.Status is GenericStatusType.None ? GenericStatusType.Pending : request.Status);
        
        await _dataLayer.HealthEssentialsContext.LaboratoryMembers.AddAsync(laboratoryMember, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory member with Guid {laboratoryMember.Guid} has been created",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = new()
            {
                Guid = Guid.Parse(laboratoryMember.Guid)
            }
        };
    }
}