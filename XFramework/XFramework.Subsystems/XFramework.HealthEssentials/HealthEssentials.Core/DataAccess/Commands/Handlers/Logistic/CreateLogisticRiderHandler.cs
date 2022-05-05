using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using Mapster;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticRiderHandler : CommandBaseHandler, IRequestHandler<CreateLogisticRiderCmd, CmdResponse<CreateLogisticRiderCmd>>
{
    public CreateLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticRiderCmd>> Handle(CreateLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        var logistic = await _dataLayer.HealthEssentialsContext.Logistics
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.TypeGuid}", cancellationToken: cancellationToken);
       
        if (logistic is null)
        {
            return new ()
            {
                Message = $"Logistic with Guid {request.TypeGuid} does not exist",
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

        var contactGroup = _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_RIDER", cancellationToken: CancellationToken.None);
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
        
        var entity = request.Adapt<LogisticRider>();
        entity.CredentialId = credential.Id;
        entity.Name = request.ProfessionalName;
        entity.Description = request.Description;

        var riderHandle = new LogisticRiderHandle
        {
            LogisticId = logistic.Id,
            LogisticRider = entity,
            Status = (short) GenericStatusType.Approved,
            Guid = $"{Guid.NewGuid()}"
        };

        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);
        
        await _dataLayer.HealthEssentialsContext.LogisticRiders.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.LogisticRiderHandles.AddAsync(riderHandle, CancellationToken.None);

        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}