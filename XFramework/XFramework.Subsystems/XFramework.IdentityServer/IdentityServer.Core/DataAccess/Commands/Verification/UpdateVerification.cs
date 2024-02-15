using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Verification;

public class UpdateVerification(
        DbContext dbContext,
        ITenantService tenantService,
        ILogger<UpdateVerification> logger,
        IRequestHandler<Patch<IdentityVerification>, CmdResponse<IdentityVerification>> baseHandler
    ) 
    : IPatchHandler<IdentityVerification>, IDecorator
{
    public async Task<CmdResponse<IdentityVerification>> Handle(Patch<IdentityVerification> request, CancellationToken cancellationToken)
    {
        var verification = await dbContext.Set<IdentityVerification>()
            .Where(i => i.Status == (int?) GenericStatusType.Pending)
            .Where(i => i.Token == request.Model.Token)
            .FirstOrDefaultAsync(CancellationToken.None);
        if (verification == null)
        {
            return new ()
            {
                Message = $"Verification with token {request.Model.Token} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        verification.Status = (short?) GenericStatusType.Approved;

        await baseHandler.Handle(new Patch<IdentityVerification>(verification) , cancellationToken);
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}