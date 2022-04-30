using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Verification;

public class CreateVerificationHandler : CommandBaseHandler, IRequestHandler<CreateVerificationCmd, CmdResponse>
{

    public CreateVerificationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse> Handle(CreateVerificationCmd request, CancellationToken cancellationToken)
    {
        var verificationType = await _dataLayer.IdentityVerificationEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Guid == $"{request.VerificationTypeGuid}", CancellationToken.None);
        if (verificationType == null)
        {
            return new()
            {
                Message = $"Verification type with guid {request.VerificationTypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        verificationType.
    }
}