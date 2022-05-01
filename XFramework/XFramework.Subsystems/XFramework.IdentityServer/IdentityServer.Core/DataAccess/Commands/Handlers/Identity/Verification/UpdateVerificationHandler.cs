using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Verification;

public class UpdateVerificationHandler : CommandBaseHandler, IRequestHandler<UpdateVerificationCmd, CmdResponse>
{
    public UpdateVerificationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    
    public async Task<CmdResponse> Handle(UpdateVerificationCmd request, CancellationToken cancellationToken)
    {
        var verification = await _dataLayer.IdentityVerifications
            .Where(i => i.Status == (int?) GenericStatusType.Pending)
            .Where(i => i.Token == $"{request.VerificationCode}")
            .FirstOrDefaultAsync(CancellationToken.None);
        if (verification == null)
        {
            return new ()
            {
                Message = $"Verification with token {request.VerificationCode} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        verification.Status = (short?) GenericStatusType.Approved;
        _dataLayer.IdentityVerifications.Update(verification);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}