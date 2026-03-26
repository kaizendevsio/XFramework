using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Search;

public static class SearchIdentitiesEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/identities", Tags = ["Community Identities"],
        Summary = "Search community identities",
        Description = "Searches community identities by HandleName or Alias with optional TypeId filter. Returns paginated results.")]
    public static async Task<Result<PaginatedResult<SearchIdentitiesResponse>>> Handle(
        SearchIdentitiesRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.SearchIdentitiesAsync(request, ct);
    }
}
