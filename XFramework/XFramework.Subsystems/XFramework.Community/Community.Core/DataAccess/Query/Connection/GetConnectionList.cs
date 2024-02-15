namespace Community.Core.DataAccess.Query.Connection;

public class GetConnectionList(
    AppDbContext appDbContext,
    ILogger<GetConnectionList> logger,
    ITenantService tenantService
    ) 
    : IRequestHandler<GetCommunityConnectionListRequest, QueryResponse<List<CommunityConnection>>>
{
    public async Task<QueryResponse<List<CommunityConnection>>> Handle(GetCommunityConnectionListRequest request, CancellationToken cancellationToken)
    {
        var connectionType = await appDbContext.CommunityConnectionTypes.FirstOrDefaultAsync(i => i.Id == request.ConnectionTypeId, cancellationToken: cancellationToken);
        if (connectionType == null)
        {
            return new ()
            {
                Message = $"Connection entity with id {request.ConnectionTypeId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }
        
        var communityIdentity = await appDbContext.CommunityIdentities.FirstOrDefaultAsync(i => i.Id == request.CommunityIdentityId, cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with id {request.CommunityIdentityId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        var connectionList = await appDbContext.CommunityConnections
            .Include(i => i.SourceSocialMediaIdentity)
            .Include(i => i.TargetSocialMediaIdentity)
            .Where(i => i.TypeId == connectionType.Id)
            .Where(i => i.SourceSocialMediaIdentityId == communityIdentity.Id || i.TargetSocialMediaIdentityId == communityIdentity.Id)
            .Take(request.Limit)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None);
        
        if (!connectionList.Any())
        {
            return new ()
            {
                Message = $"No connections exist",
                HttpStatusCode = HttpStatusCode.NoContent,
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = connectionList
        };
    }
}