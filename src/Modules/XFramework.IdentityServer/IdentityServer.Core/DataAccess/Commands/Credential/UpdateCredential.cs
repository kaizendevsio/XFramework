using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Credential;

public class UpdateCredential(
        DbContext appDbContext,
        ILogger<UpdateCredential> logger,
        ITenantService tenantService,
        IRequestHandler<Patch<IdentityCredential>, CmdResponse<IdentityCredential>> baseHandler
    )
    : IPatchHandler<IdentityCredential>, IDecorator
{
    public async Task<CmdResponse<IdentityCredential>> Handle(Patch<IdentityCredential> request, CancellationToken cancellationToken)
    {
        return await baseHandler.Handle(request, cancellationToken);
    }
}