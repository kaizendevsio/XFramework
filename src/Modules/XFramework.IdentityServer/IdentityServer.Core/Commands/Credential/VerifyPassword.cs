using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;

namespace IdentityServer.Core.Commands.Credential;

public class VerifyPassword(
    DbContext appDbContext,
    ILogger<CreateCredential> logger,
    ITenantService tenantService
)
    : IRequestHandler<VerifyPasswordRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(VerifyPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Password))
        {
            logger.LogWarning("Attempt to verify password with a null or empty password");
            throw new ArgumentException("Please provide a valid password");
        }

        if (request.CredentialId == Guid.Empty)
        {
            logger.LogWarning("Attempt to verify password with an empty identifier");
            throw new ArgumentException("An error occurred while processing your request");
        }
        
        try
        {
            var user = await appDbContext.Set<IdentityCredential>()
                           .FirstOrDefaultAsync(u => u.Id == request.CredentialId, CancellationToken.None);

            if (user == null)
            {
                logger.LogWarning("User not found with identifier {Identifier}", request.CredentialId);
                return new CmdResponse
                {
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Message = "User not found"
                };
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, Encoding.ASCII.GetString(user.PasswordByte));

            if (!isPasswordValid)
            {
                logger.LogWarning("Invalid password for user with identifier {Identifier}", request.CredentialId);
                return new CmdResponse
                {
                    HttpStatusCode = HttpStatusCode.BadRequest,
                    Message = "Invalid password"
                };
            }

            logger.LogInformation("Password verification successful for user with identifier {Identifier}", request.CredentialId);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while verifying password for user with identifier {Identifier}", request.CredentialId);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                Message = "An error occurred while processing your request"
            };
        }
    }
}