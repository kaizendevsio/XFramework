using Community.Core.DataAccess.Commands.Entity.Identity;
using Community.Domain.Generic.Enums;

namespace Community.Core.DataAccess.Commands.Handlers.Identity;

public class UpdateIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateCommunityIdentityCmd, CmdResponse>
{
    public UpdateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(UpdateCommunityIdentityCmd request, CancellationToken cancellationToken)
    {
        var communityIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        var credential = new IdentityCredential();
        if (request.CredentialGuid is not null)
        {
            credential = await _dataLayer.IdentityCredentials
                .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
         
            if (credential == null)
            {
                return new ()
                {
                    Message = $"Credential with guid {request.CredentialGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    
                };
            }
        }

        var communityIdentityEntity = new CommunityIdentityEntity();
        if (request.CommunityIdentityEntityGuid is not null)
        {
            communityIdentityEntity = await _dataLayer.CommunityIdentityEntities
                .FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityEntityGuid}", cancellationToken: cancellationToken);
         
            if (communityIdentityEntity == null)
            {
                return new ()
                {
                    Message = $"Community Identity Entity with guid {request.CommunityIdentityEntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    
                };
            }
        }


        communityIdentity = request.Adapt(communityIdentity);
        if (request.CommunityIdentityEntityGuid is not null)
        {
            communityIdentity.Entity = communityIdentityEntity;
        }
        if (request.CredentialGuid is not null)
        {
            communityIdentity.IdentityCredential = credential;
        }

        _dataLayer.CommunityIdentities.Update(communityIdentity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}