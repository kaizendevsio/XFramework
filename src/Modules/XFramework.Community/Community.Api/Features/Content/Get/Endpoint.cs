using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Get;

public static class GetContentEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/content/{id:guid}", Tags = ["Community Content"],
        Summary = "Get content by ID",
        Description = "Returns content with author info, reaction count, and comment count.")]
    public static async Task<Result<GetContentResponse>> Handle(
        GetContentRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.GetContentAsync(request, ct);
    }
}
