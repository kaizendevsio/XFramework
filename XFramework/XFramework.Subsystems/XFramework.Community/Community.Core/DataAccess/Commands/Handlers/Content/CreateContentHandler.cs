using Community.Core.DataAccess.Commands.Entity.Content;
using Community.Core.DataAccess.Commands.Entity.Identity;

namespace Community.Core.DataAccess.Commands.Handlers.Content;

public class CreateContentHandler : CommandBaseHandler, IRequestHandler<CreateContentCmd, CmdResponse>
{
    public CreateContentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateContentCmd request, CancellationToken cancellationToken)
    {
        var communityIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityGuid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.CommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        var contentEntity = await _dataLayer.CommunityContentEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.ContentEntityGuid}", cancellationToken: cancellationToken);
        if (contentEntity == null)
        {
            return new ()
            {
                Message = $"Content entity with guid {request.ContentEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        CommunityContent? parentContent = null;
        if (request.ParentContentGuid is not null)
        {
            parentContent = await _dataLayer.CommunityContents
                .FirstOrDefaultAsync(i => i.Guid == $"{request.ParentContentGuid}", cancellationToken: cancellationToken);
            if (parentContent == null)
            {
                return new ()
                {
                    Message = $"Parent content with guid {request.ParentContentGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    
                };
            }
        }

        CommunityIdentity? communityGroupIdentity = null;
        if (request.CommunityGroupGuid is not null)
        {
            communityGroupIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityGroupGuid}", cancellationToken: cancellationToken);
            if (communityGroupIdentity == null)
            {
                return new ()
                {
                    Message = $"Group with guid {request.CommunityGroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    
                };
            }
        }
        
        var newContent = request.Adapt<CommunityContent>();
        newContent.Entity = contentEntity;
        newContent.SocialMediaIdentity = communityIdentity;

        if (parentContent is not null)
        {
            newContent.ParentContent = parentContent;
        }
        if (request.Guid is not null)
        {
            newContent.Guid = $"{request.Guid}";
        }
        if (request.CommunityGroupGuid is not null)
        {
            newContent.CommunityGroup = communityGroupIdentity;
        }

        _dataLayer.CommunityContents.Add(newContent);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}