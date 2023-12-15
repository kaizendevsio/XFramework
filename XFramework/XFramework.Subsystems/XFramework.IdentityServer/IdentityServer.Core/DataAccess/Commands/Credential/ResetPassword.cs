using IdentityServer.Domain.Generic;
using IdentityServer.Domain.Generic.Contracts.Requests;
using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class ResetPassword(
    AppDbContext appDbContext,
    ILogger<CreateCredential> logger,
    IMediator mediator,
    ITenantService tenantService
)
    : IRequestHandler<ResetPasswordRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.PhoneEmailUsername))
        {
            logger.LogWarning("Attempt to reset password with a null or empty identifier");
            throw new ArgumentException("An error occurred while processing your request");
        }

        try
        {
            var user = await appDbContext.IdentityCredentials
                           .FirstOrDefaultAsync(u => u.UserName == request.PhoneEmailUsername, CancellationToken.None)
                       ?? await appDbContext.IdentityContacts
                           .Where(u => u.Value == request.PhoneEmailUsername)
                           .Select(i => i.Credential)
                           .FirstOrDefaultAsync(CancellationToken.None);

            if (user == null)
            {
                logger.LogWarning("User not found with identifier {Identifier}", request.PhoneEmailUsername);
                return new CmdResponse
                {
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Message = "User not found"
                };
            }

            // Reset the user's password logic goes here
            await mediator.Send(new Create<IdentityVerification>(new IdentityVerification
            {
                CredentialId = user.Id,
                TenantId = user.TenantId,
                VerificationTypeId = IdentityConstants.VerificationType.Sms,
                Status = (short?) GenericStatusType.Pending
            }));

            await appDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Password reset request successful for identifier {Identifier}", request.PhoneEmailUsername);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Password reset request successful"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while resetting password for identifier {Identifier}", request.PhoneEmailUsername);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}