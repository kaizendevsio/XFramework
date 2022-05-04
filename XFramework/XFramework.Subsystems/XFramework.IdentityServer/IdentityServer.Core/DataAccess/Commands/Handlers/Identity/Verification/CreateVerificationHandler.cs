using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Verification;

public class CreateVerificationHandler : CommandBaseHandler, IRequestHandler<CreateVerificationCmd, CmdResponse>
{
    private readonly IHelperService _helperService;
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;

    public CreateVerificationHandler(IDataLayer dataLayer, IHelperService helperService, IMessagingServiceWrapper messagingServiceWrapper)
    {
        _helperService = helperService;
        _messagingServiceWrapper = messagingServiceWrapper;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse> Handle(CreateVerificationCmd request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        
        var verificationType = await _dataLayer.IdentityVerificationEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.VerificationTypeGuid}", CancellationToken.None);
        if (verificationType == null)
        {
            return new()
            {
                Message = $"Verification type with guid {request.VerificationTypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var identityCredential = await _dataLayer.IdentityCredentials
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (identityCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        switch (verificationType.Name)
        {
            case "SMS":
                var messageTemplate = await _dataLayer.RegistryConfigurations
                    .Where(i => i.ApplicationId == application.Id)
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
                var contact = identityCredential.IdentityContacts?.FirstOrDefault(i => i.Entity.Name == "Phone")?.Value;

                if (string.IsNullOrEmpty(contact))
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadGateway,
                        IsSuccess = false,
                        Message = $"Credential with guid {request.CredentialGuid} does not have a phone number"
                    };
                }
                
                _dataLayer.IdentityVerifications.Add(new()
                {
                    Status = (short?) GenericStatusType.Pending,
                    StatusUpdatedOn = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
                    Token = $"{otp}",
                    Expiry = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime().AddMinutes((double) verificationType.DefaultExpiry), DateTimeKind.Utc),
                    IdentityCred = identityCredential,
                    VerificationType = verificationType
                });
                
                await _messagingServiceWrapper.CreateDirectMessage(new()
                {
                    MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
                    Sender = "+630000000000",
                    Recipient = contact,
                    Subject = "One Time Password",
                    Intent = "OTP",
                    Message = message,
                    IsScheduled = false
                });
                
                await _dataLayer.SaveChangesAsync(CancellationToken.None);
                break;
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}