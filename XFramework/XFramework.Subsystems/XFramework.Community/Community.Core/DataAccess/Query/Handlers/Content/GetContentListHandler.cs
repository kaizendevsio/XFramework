using Community.Core.DataAccess.Query.Entity.Content;
using Community.Domain.Generic.Contracts.Responses.Content;
using Community.Domain.Generic.Contracts.Responses.Identity;

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
            .ThenInclude(i => i.IdentityCredential)
            .Include(i => i.SocialMediaIdentity)
            .ThenInclude(i => i.CommunityIdentityFiles)
            .ThenInclude(i => i.Entity)
            .Include(i => i.CommunityContentFiles)
            .Include(i => i.CommunityContentReactions)
            .ThenInclude(i => i.Entity)
            .Include(i => i.Entity)
            .Include(i => i.InverseParentContent)
            .ThenInclude(i => i.Entity)
            .Where(i => i.Entity.Guid == $"{request.ContentEntityGuid}")
            .Where(i => i.CreatedAt > request.GreaterThan)
            .Where(i => i.IsDeleted == false)
            .Where(i => i.IsEnabled == true)
            .OrderByDescending(i => i.CreatedAt)
            .Take(request.Limit)
            .AsSplitQuery()
            .AsNoTracking()
            .Select(i => new CommunityContentResponse
            {
                CreatedAt = i.CreatedAt,
                ModifiedAt = i.ModifiedAt,
                Title = i.Title,
                Text = i.Text,
                SocialMediaIdentityGuid = Guid.Parse(i.SocialMediaIdentity.Guid),
                EntityId = i.EntityId,
                ParentContentGuid = i.ParentContent == null ? null : Guid.Parse(i.ParentContent.Guid),
                Guid = Guid.Parse(i.Guid),
                CommunityIdentity = new()
                {
                    Tagline = i.SocialMediaIdentity.Tagline,
                    Alias = i.SocialMediaIdentity.Alias,
                    HandleName = i.SocialMediaIdentity.HandleName,
                    Status = i.SocialMediaIdentity.Status,
                    LastActive = i.SocialMediaIdentity.LastActive,
                    Guid = i.SocialMediaIdentity.Guid,
                    EntityGuid = Guid.Parse(i.SocialMediaIdentity.Entity.Guid),
                    IdentityCredentialGuid = Guid.Parse(i.SocialMediaIdentity.IdentityCredential.Guid)
                },
                Entity = i.Entity.Adapt<CommunityContentEntityResponse>(),
                Files = i.CommunityContentFiles.Adapt<List<CommunityContentFileResponse>>(),
                Comments = i.InverseParentContent.Where(o => o.Entity.Guid == "78a49ca4-248a-4f05-86d0-80b8a772eec8").Select(o => new CommunityContentResponse
                {
                    CreatedAt = o.CreatedAt,
                    ModifiedAt = o.ModifiedAt,
                    IsEnabled = o.IsEnabled,
                    IsDeleted = o.IsDeleted,
                    Title = o.Text,
                    Text = o.Title
                }).ToList(),
                Reactions = i.CommunityContentReactions.Select(o => new CommunityContentReactionResponse
                {
                    CreatedAt = o.CreatedAt,
                    ModifiedAt = o.ModifiedAt,
                    IsEnabled = o.IsEnabled,
                    IsDeleted = o.IsDeleted,
                    Guid = Guid.Parse(o.Guid),
                    CommunityIdentity = o.SocialMediaIdentity.Adapt<CommunityIdentityResponse>(),
                    Entity = o.Entity.Adapt<CommunityContentReactionEntityResponse>()
                }).ToList()
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