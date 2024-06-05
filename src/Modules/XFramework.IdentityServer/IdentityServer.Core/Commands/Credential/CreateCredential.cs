using XFramework.Core.Services;

namespace IdentityServer.Core.Commands.Credential;

public class CreateCredential(
    DbContext appDbContext,
    ILogger<CreateCredential> logger,
    ITenantService tenantService,
    IRequestHandler<Create<IdentityCredential>, CmdResponse<IdentityCredential>> baseHandler
)
    : ICreateHandler<IdentityCredential>, IDecorator
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Create<IdentityCredential> request,
        CancellationToken cancellationToken)
    {
        var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.Model.Password, workFactor: 11));
        request.Model.PasswordByte = hashPasswordByte;

        return await baseHandler.Handle(request, cancellationToken);
    }
}