using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts;

public class CreateContactHandler : CommandBaseHandler, IRequestHandler<CreateContactCmd,CmdResponse<CreateContactCmd>>
{
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;
    private readonly IHelperService _helperService;

    public CreateContactHandler(IDataLayer dataLayer, IMessagingServiceWrapper messagingServiceWrapper, IHelperService helperService)
    {
        _messagingServiceWrapper = messagingServiceWrapper;
        _helperService = helperService;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateContactCmd>> Handle(CreateContactCmd request, CancellationToken cancellationToken)
    {
        var identityCredential = await _dataLayer.IdentityCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (identityCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var contactEntity = await _dataLayer.IdentityContactEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == (long)request.ContactType ,cancellationToken);
        if (contactEntity == null)
        {
            return new ()
            {
                Message = $"Contact entity with ID {request.ContactType} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var existingContact = _dataLayer.IdentityContacts.Any(i => i.Value == request.Value);
        if (existingContact)
        {
            return new ()
            {
                Message = $"The {request.ContactType} '{request.Value}' already exist",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        switch (request.ContactType)
        {
            case GenericContactType.NotSpecified:
                break;
            case GenericContactType.Email:
                request.Value.ValidateEmailAddress();
                break;
            case GenericContactType.Phone:
                request.Value = request.Value.ValidatePhoneNumber();
                break;
        }
            
        var contact = new IdentityContact()
        {
            UserCredentialId = identityCredential.Id,
            UcentitiesId = contactEntity.Id,
            Value = request.Value
        };

        _dataLayer.IdentityContacts.Add(contact);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        if (request.SendOtp is not true)
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }

        var messageTemplate = await _dataLayer.RegistryConfigurations
            .Where(i => i.ApplicationId == identityCredential.ApplicationId)
            .Where(i => i.Group.Name == "MessagingService_Otp")
            .FirstOrDefaultAsync(CancellationToken.None);

        if (messageTemplate is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Conflict,
                IsSuccess = true,
                Message = "Unable to send message: OTP message template could not be found"
            };
        }

        var otp = _helperService.GenerateRandomNumber(111111, 999999);
        var message = messageTemplate.Value.Replace("|Value|", $"{otp}");

        var verificationEntity =
            await _dataLayer.IdentityVerificationEntities.FirstOrDefaultAsync(i => i.Name == "SMS",
                CancellationToken.None);

        _dataLayer.IdentityVerifications.Add(new()
        {
            Status = (short?) GenericStatusType.Pending,
            StatusUpdatedOn = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
            Token = $"{otp}",
            Expiry = DateTime.SpecifyKind(
                DateTime.Now.ToUniversalTime().AddMinutes((double) verificationEntity.DefaultExpiry), DateTimeKind.Utc),
            IdentityCred = identityCredential,
            VerificationType = verificationEntity
        });

        Task.Run(async () =>
        {
            await _messagingServiceWrapper.CreateDirectMessage(new()
            {
                MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
                Sender = "+630000000000",
                Recipient = contact.Value,
                Subject = "One Time Password",
                Intent = "OTP",
                Message = message,
                IsScheduled = false
            });
        });
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}