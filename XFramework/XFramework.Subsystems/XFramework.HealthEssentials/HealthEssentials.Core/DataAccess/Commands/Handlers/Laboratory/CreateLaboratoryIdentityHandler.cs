using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using Microsoft.EntityFrameworkCore;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryIdentityHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryIdentityCmd, CmdResponse<CreateLaboratoryIdentityCmd>>
{
    public CreateLaboratoryIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryIdentityCmd>> Handle(CreateLaboratoryIdentityCmd request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.LaboratoryGuid}", cancellationToken: cancellationToken);
       
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.CredentialGuid} does not exist",
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

        var contactGroup = _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_LABORATORY", cancellationToken: CancellationToken.None);
        var emailContactType = _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Email", cancellationToken: CancellationToken.None);
        var phoneContactType = _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Phone", cancellationToken: CancellationToken.None);

        await Task.WhenAll(contactGroup, emailContactType, phoneContactType);
        
        var workEmail = new IdentityContact
        {
            EntityId = emailContactType.Result.Id,
            Value = request.Email,
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Result.Id,
        };

        var workPhone = new IdentityContact
        {
            EntityId = phoneContactType.Result.Id,
            Value = request.PhoneNumber.ValidatePhoneNumber(convertOnly: true),
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Result.Id,
        };
        
        var entity = new LaboratoryMember
        {
            CredentialId = credential.Id,
            LaboratoryId = laboratory.Id,
            Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}",
            Name = request.ProfessionalName
        };
        
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);
        
        await _dataLayer.HealthEssentialsContext.LaboratoryMembers.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory member with Guid {entity.Guid} has been created",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}