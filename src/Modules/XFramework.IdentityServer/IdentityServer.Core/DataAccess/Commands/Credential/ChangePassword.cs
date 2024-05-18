using IdentityServer.Core.DataAccess.Commands.Verification;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Messaging.Integration.Drivers;
using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class ChangePassword(
    DbContext dbContext,
    ILogger<CreateCredential> logger,
    IMessagingServiceWrapper messagingServiceWrapper,
    IMediator mediator,
    
    ITenantService tenantService
)
    : IRequestHandler<ChangePasswordRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.CreadentialId == Guid.Empty)
        {
            logger.LogWarning("Attempt to reset password with a null or empty identifier");
            throw new ArgumentException("Identifier is required");
        }
        
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            logger.LogWarning("Attempt to reset password with a null or empty password");
            throw new ArgumentException("Password is required");
        }

        var credential = await dbContext.Set<IdentityCredential>()
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Type)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == request.CreadentialId, CancellationToken.None);

        if (credential == null)
        {
            logger.LogWarning("Credential with ID {CredentialId} not found", request.CreadentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "User not found"
            };
        }

        var verification = await dbContext.Set<IdentityVerification>()
            .Where(i => i.VerificationType.Name == nameof(IdentityConstants.VerificationType.Sms))
            .Where(i => i.Status == (int)GenericStatusType.Approved)
            .Where(i => i.Id == request.VerificationId)
            .Where(i => i.StatusUpdatedOn >= DateTime.Now.ToUniversalTime().AddMinutes(-10))
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (verification == null)
        {
            logger.LogWarning("Invalid verification code or expired for credential {CredentialId}", request.CreadentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "Invalid verification code or expired"
            };
        }
        
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.NewPassword, workFactor:11));
        credential.PasswordByte = hashPasswordByte;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Password reset request successful for credential {CredentialId}", request.CreadentialId);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Password reset request successful"
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while resetting password for credential {CredentialId}", request.CreadentialId);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while resetting password for credential {CredentialId}", request.CreadentialId);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
     
        
    }
}