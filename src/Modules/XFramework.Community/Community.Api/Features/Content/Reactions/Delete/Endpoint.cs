using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Reactions.Delete;

public static class DeleteContentReactionEndpoint
{
    [BoltHandler]
    [MapDelete("/api/community/content/{contentId:guid}/reactions/{reactionId:guid}", Tags = ["Community Content Reactions"],
        Summary = "Remove a reaction",
        Description = "Soft-deletes a reaction. Validates that the requester owns the reaction.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteContentReactionRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.DeleteContentReactionAsync(request, ct);
    }
}
