using Community.Core.DataAccess.Commands.Entity.Identity;
using Community.Domain.Generic.Enums;

namespace Community.Core.DataAccess.Commands.Handlers.Identity;

public class UpdateIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateIdentityCmd, CmdResponse>
{
    public UpdateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(UpdateIdentityCmd request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.IdentityCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
     
        if (credential == null)
        {
            return new ()
            {
                Message = $"Credential with guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var communityIdentityEntity = await _dataLayer.CommunityIdentityEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityEntityGuid}", cancellationToken: cancellationToken);
     
        if (communityIdentityEntity == null)
        {
            return new ()
            {
                Message = $"Community Identity Entity with guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var communityIdentity = await _dataLayer.CommunityIdentities
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
     
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        _dataLayer.CommunityIdentities.Update(communityIdentity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}