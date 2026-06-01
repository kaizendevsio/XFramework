using XFramework.Integration.Attributes;

namespace Community.Api.Features.Connections.GetList;

public static class GetConnectionListEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/connections", Tags = ["Community Connections"],
        Summary = "Get a list of connections for an identity",
        Description = "Retrieves connections for a community identity filtered by connection type",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<List<CommunityConnection>>> Handle(
        GetCommunityConnectionListRequest request,
        ICommunityService communityService,
        CancellationToken ct)
    {
        return await communityService.GetConnectionListAsync(request, ct);
    }
}
