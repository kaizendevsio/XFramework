using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Core.DataAccess.Commands.Verification;

public class UpdateVerification(
        AppDbContext appDbContext,
        ILogger<UpdateVerification> logger,
        IEnumerable<IRequestHandler<Patch<IdentityVerification>, CmdResponse<IdentityVerification>>> requestHandlers
    ) 
    : IPatchHandler<IdentityVerification>
{
    public async Task<CmdResponse<IdentityVerification>> Handle(Patch<IdentityVerification> request, CancellationToken cancellationToken)
    {
        var verification = await appDbContext.IdentityVerifications
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

        await requestHandlers.First().Handle(new Patch<IdentityVerification>(verification) , cancellationToken);
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}