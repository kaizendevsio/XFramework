using Community.Core.DataAccess.Query.Entity.Identity;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Core.DataAccess.Query.Handlers.Identity;

public class GetIdentityListHandler : QueryBaseHandler, IRequestHandler<GetIdentityListQuery, QueryResponse<List<CommunityIdentityResponse>>>
{
    public GetIdentityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<CommunityIdentityResponse>>> Handle(GetIdentityListQuery request, CancellationToken cancellationToken)
    {
        var communityIdentityEntity = await _dataLayer.CommunityIdentityEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityEntityGuid}", cancellationToken: cancellationToken);
     
        if (communityIdentityEntity == null)
        {
            return new ()
            {
                Message = $"Community identity entity with guid {request.CommunityIdentityEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        var communityIdentityList = await _dataLayer.CommunityIdentities
            .AsNoTracking()
            .Where(i => i.EntityId == communityIdentityEntity.Id)
            .OrderByDescending(i => i.CreatedAt)
            .Take(request.Limit)
            .ToListAsync();

        if (!communityIdentityList.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = true,
                Message = "No community identities found"
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = communityIdentityList.Adapt<List<CommunityIdentityResponse>>()
        };
    }
}