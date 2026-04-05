using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Reactions.Create;

public static class CreateContentReactionEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/content/{contentId:guid}/reactions", Tags = ["Community Content Reactions"],
        Summary = "React to content",
        Description = "Creates a reaction on a content item. Prevents duplicate reactions of the same type. Notifies the content author.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateContentReactionRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.CreateContentReactionAsync(request, ct);
    }
}
