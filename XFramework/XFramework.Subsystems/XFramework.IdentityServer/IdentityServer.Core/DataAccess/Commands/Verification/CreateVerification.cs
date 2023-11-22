using Messaging.Integration.Drivers;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.DataAccess.Commands.Verification;

public class CreateVerification(
        AppDbContext appDbContext,
        IHelperService helperService, 
        ILogger<CreateVerification> logger,
        IMessagingServiceWrapper messagingServiceWrapper,
        IRequestHandler<Create<IdentityVerification>, CmdResponse<IdentityVerification>> baseHandler
    ) 
    : ICreateHandler<IdentityVerification>, IDecorator
{
    public async Task<CmdResponse<IdentityVerification>> Handle(Create<IdentityVerification> request, CancellationToken cancellationToken)
    {
        var tenant = await GetTenant(request.Model.TenantId);

        var verificationType = await appDbContext.IdentityVerificationTypes
            .FirstOrDefaultAsync(i => i.Id == request.Model.VerificationTypeId, CancellationToken.None);
        if (verificationType is null)
        {
            return new()
            {
                Message = $"Verification type with id {request.Model.VerificationTypeId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
            };
        }

        var identityCredential = await appDbContext.IdentityCredentials
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
            case "SMS":
                var messageTemplate = await appDbContext.RegistryConfigurations
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
                
                await baseHandler.Handle(new Create<IdentityVerification>(new()
                {
                    Status = (short?)GenericStatusType.Pending,
                    StatusUpdatedOn = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
                    Token = $"{otp}",
                    Expiry = DateTime.SpecifyKind(
                        DateTime.Now.ToUniversalTime().AddMinutes((double)verificationType.DefaultExpiry),
                        DateTimeKind.Utc),
                    Credential = identityCredential,
                    VerificationType = verificationType
                }), cancellationToken);
                
                await messagingServiceWrapper.CreateDirectMessage(new()
                {
                    MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
                    Sender = "+630000000000",
                    Recipient = contact,
                    Subject = "One Time Password",
                    Intent = "OTP",
                    Message = message,
                    IsScheduled = false
                });

                //await appDbContext.SaveChangesAsync(CancellationToken.None);
                break;
        }
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
    
    private async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        var entity = await appDbContext.Tenants
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        ArgumentNullException.ThrowIfNull(entity, "Tenant");

        return entity;
    }
}