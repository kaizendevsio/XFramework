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
            .Include(i => i.IdentityInfo)
            .AsSplitQuery()
            
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
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityEntityGuid}", cancellationToken: cancellationToken);
     
        if (communityIdentityEntity == null)
        {
            return new ()
            {
                Message = $"Community identity entity with guid {request.CommunityEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var identityFileEntities = await _dataLayer.CommunityIdentityFileEntities.ToListAsync(CancellationToken.None);
        var storageFileEntities = await _dataLayer.StorageFileEntities.ToListAsync(CancellationToken.None);

        var pngType = storageFileEntities.FirstOrDefault(i => i.Guid == "af6b9396-ba01-4f88-a5d0-e0cfbc038146");
        var entity = new CommunityIdentity
        {
            IdentityCredential = credential,
            HandleName = string.IsNullOrEmpty(request.HandleName) ? $"{credential.IdentityInfo.FirstName} {credential.IdentityInfo.LastName}" : request.HandleName,
            Tagline = request.Tagline,
            Alias = request.Alias,
            Status = (int) CommunityIdentityStatus.Active,
            LastActive = DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc),
            Entity = communityIdentityEntity,
            CommunityIdentityFiles = new List<CommunityIdentityFile>()
            {
                // Profile Photo
                new()
                {
                    Entity = identityFileEntities.FirstOrDefault(i => i.Guid == "996dd417-170c-4ac9-b565-62caf4ab5ccf"),
                    Storage = new()
                    {
                        ContentPath = "",
                        Entity = pngType
                    }
                },
                // Cover Photo
                new()
                {
                    Entity = identityFileEntities.FirstOrDefault(i => i.Guid == "8716ec30-b061-45cc-ad5b-77bda960d90e"),
                    Storage = new()
                    {
                        ContentPath = "",
                        Entity = pngType
                    }
                }
            }
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