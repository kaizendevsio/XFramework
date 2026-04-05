using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Get;

public static class GetCommunityIdentityEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/identities/{id:guid}", Tags = ["Community Identities"],
        Summary = "Get community identity profile",
        Description = "Returns identity profile with HandleName, Tagline, Alias, Status, follower count, following count, and content count.")]
    public static async Task<Result<GetCommunityIdentityResponse>> Handle(
        GetCommunityIdentityRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.GetCommunityIdentityAsync(request, ct);
    }
}
