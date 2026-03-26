using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Feed.GetFeed;

public static class GetFeedEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/feed", Tags = ["Community Feed"],
        Summary = "Get the feed/timeline for an identity",
        Description = "Returns a paginated feed of content from followed users and the identity's own content, ordered by most recent.")]
    public static async Task<Result<GetFeedResponse>> Handle(
        GetFeedRequest request,
        IFeedService feedService,
        CancellationToken ct)
    {
        return await feedService.GetFeedAsync(request, ct);
    }
}
