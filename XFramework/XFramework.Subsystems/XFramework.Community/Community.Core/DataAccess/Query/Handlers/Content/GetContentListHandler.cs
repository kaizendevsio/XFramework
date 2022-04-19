using Community.Core.DataAccess.Query.Entity.Content;
using Community.Domain.Generic.Contracts.Responses.Content;

namespace Community.Core.DataAccess.Query.Handlers.Content;

public class GetContentListHandler : QueryBaseHandler, IRequestHandler<GetContentListQuery, QueryResponse<List<CommunityContentResponse>>>
{
    public GetContentListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<CommunityContentResponse>>> Handle(GetContentListQuery request, CancellationToken cancellationToken)
    {
        var communityIdentity = await _dataLayer.CommunityIdentities
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityGuid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.CommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var contentEntity = await _dataLayer.CommunityContentEntities
            .FirstOrDefaultAsync(i => i.Guid == $"{request.ContentEntityGuid}", cancellationToken: cancellationToken);
        if (contentEntity == null)
        {
            return new ()
            {
                Message = $"Content entity with guid {request.ContentEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var communityContents = await _dataLayer.CommunityContents
            .Include(i => i.ParentContent)
            .Include(i => i.SocialMediaIdentity)
            .Include(i => i.CommunityContentFiles)
            .Include(i => i.CommunityContentReactions)
            .Include(i => i.Entity)
            .Where(i => i.Guid == $"{request.ContentEntityGuid}")
            .AsSplitQuery()
            .AsNoTracking()
            .Select(i => new CommunityContentResponse
            {
                Title = i.Title,
                Text = i.Text,
                SocialMediaIdentityGuid = i.SocialMediaIdentity.Id,
                EntityId = i.EntityId,
                ParentContentId = i.ParentContentId,
                Guid = i.Guid,
                CommunityIdentity = i.SocialMediaIdentity.Adapt<CommunityIdentityResponse>(),
                ContentEntity = i.Entity.Adapt<CommunityContentEntityResponse>(),
                CommunityContentFiles = i.CommunityContentFiles.Adapt<List<CommunityContentFileResponse>>(),
                CommunityContentReactions = i.CommunityContentReactions.Adapt<List<CommunityContentReactionResponse>>()
            })
            .ToListAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = communityContents
        };
    }
}