using Community.Core.DataAccess.Query.Entity.Connection;
using Community.Domain.Generic.Contracts.Responses.Connection;

namespace Community.Core.DataAccess.Query.Handlers.Connection;

public class GetConnectionListHandler : QueryBaseHandler, IRequestHandler<GetConnectionListQuery, QueryResponse<List<CommunityConnectionResponse>>>
{
    public GetConnectionListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<CommunityConnectionResponse>>> Handle(GetConnectionListQuery request, CancellationToken cancellationToken)
    {
        var connectionEntity = await _dataLayer.CommunityConnectionEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.ConnectionEntityGuid}", cancellationToken: cancellationToken);
        if (connectionEntity == null)
        {
            return new ()
            {
                Message = $"Connection entity with guid {request.ConnectionEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }
        
        var communityIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityGuid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.CommunityIdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var connectionList = await _dataLayer.CommunityConnections
            .Include(i => i.SourceSocialMediaIdentity)
            .Include(i => i.TargetSocialMediaIdentity)
            .Where(i => i.EntityId == connectionEntity.Id)
            .Where(i => i.SourceSocialMediaIdentityId == communityIdentity.Id || i.TargetSocialMediaIdentityId == communityIdentity.Id)
            .Take(request.Limit)
            .AsNoTracking()
            .AsSplitQuery()
            .Select(i => new CommunityConnectionResponse
            {
                CreatedAt = i.CreatedAt,
                ModifiedAt = i.ModifiedAt,
                IsEnabled = i.IsEnabled,
                IsDeleted = i.IsDeleted,
                SourceCommunityIdentityGuid = Guid.Parse(i.SourceSocialMediaIdentity.Guid),
                TargetCommunityIdentityGuid = Guid.Parse(i.TargetSocialMediaIdentity.Guid),
                Guid = Guid.Parse(i.Guid),
                Entity = i.Entity.Adapt<CommunityConnectionEntityResponse>()
            })
            .ToListAsync(CancellationToken.None);
        
        if (!connectionList.Any())
        {
            return new ()
            {
                Message = $"No connections exist",
                HttpStatusCode = HttpStatusCode.NoContent,
                IsSuccess = false
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = connectionList
        };
    }
}