using IdentityServer.Domain.Shared;
using Messaging.Domain.Shared;
using Messaging.Integration.Drivers;
using XFramework.Core.Services;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.Commands.Verification;

public class CreateVerification(
        DbContext dbContext,
        IHelperService helperService, 
        ITenantService tenantService,
        ILogger<CreateVerification> logger,
        IMessagingServiceWrapper messagingServiceWrapper,
        IRequestHandler<Create<IdentityVerification>, CmdResponse<IdentityVerification>> baseHandler
    ) 
    : ICreateHandler<IdentityVerification>, IDecorator
{
    public async Task<CmdResponse<IdentityVerification>> Handle(Create<IdentityVerification> request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.Model.TenantId);

        var verificationType = await dbContext.Set<IdentityVerificationType>()
            .FirstOrDefaultAsync(i => i.Id == request.Model.VerificationTypeId, CancellationToken.None);
        if (verificationType is null)
        {
            return new()
            {
                Message = $"Verification type with id {request.Model.VerificationTypeId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
            };
        }

        var identityCredential = await dbContext.Set<IdentityCredential>()
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Type)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.Model.CredentialId, cancellationToken: cancellationToken);

        if (identityCredential == null)
        {
            return new()
            {
                Message = $"Credential with id {request.Model.CredentialId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        switch (verificationType.Name)
        {
            case nameof(IdentityConstants.VerificationType.Sms):
                var messageTemplate = await dbContext.Set<RegistryConfiguration>()
                    .Where(i => i.TenantId == tenant.Id)
                    .Where(i => i.Group.Name == "MessagingService_Otp")
                    .FirstOrDefaultAsync(CancellationToken.None);

                if (messageTemplate is null)
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.Conflict,
                        Message = "Unable to send message: OTP message template could not be found"
                    };
                }

                var otp = helperService.GenerateRandomNumber(111111, 999999);
                var message = messageTemplate.Value.Replace("|Value|", $"{otp}");
                var contact = identityCredential.IdentityContacts?.FirstOrDefault(i => i.Type.Name == "Phone")?.Value;

                if (string.IsNullOrEmpty(contact))
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadGateway,
                        Message = $"Credential with id {request.Model.CredentialId} does not have a phone number"
                    };
                }
                
                var result = await baseHandler.Handle(new Create<IdentityVerification>(new()
                {
                    Status = (short?)GenericStatusType.Pending,
                    StatusUpdatedOn = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
                    Token = $"{otp}",
                    Expiry = DateTime.SpecifyKind(
                        DateTime.Now.ToUniversalTime().AddMinutes((double)verificationType.DefaultExpiry),
                        DateTimeKind.Utc),
                    CredentialId = identityCredential.Id,
                    VerificationTypeId = verificationType.Id
                })
                {
                    Metadata = request.Metadata
                }, cancellationToken);
                
                await messagingServiceWrapper.CreateDirectMessage(new()
                {
                    MessageTransportType = MessageTransportType.Sms,
                    Sender = GenericSender.System,
                    Recipient = contact,
                    Subject = "One Time Password",
                    Intent = "OTP",
                    Message = message,
                    IsScheduled = false,
                    Metadata = request.Metadata
                });

                return result;
                break;
        }
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.InternalServerError,
            Message = "Verification type not supported"
        };
    }
}