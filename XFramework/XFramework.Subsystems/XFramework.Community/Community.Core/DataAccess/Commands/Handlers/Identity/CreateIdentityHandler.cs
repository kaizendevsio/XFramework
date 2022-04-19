using Community.Core.DataAccess.Commands.Entity.Identity;
using Community.Domain.Generic.Enums;

namespace Community.Core.DataAccess.Commands.Handlers.Identity;

public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd, CmdResponse>
{
    public CreateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
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
                Message = $"Community identity entity with guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var entity = new CommunityIdentity
        {
            IdentityCredential = credential,
            HandleName = null,
            Status = (int) CommunityIdentityStatus.Active,
            LastActive = DateTime.Now,
            Entity = communityIdentityEntity
        };

        _dataLayer.CommunityIdentities.Add(entity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}