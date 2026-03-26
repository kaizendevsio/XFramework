using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Delete;

public static class DeleteContentEndpoint
{
    [BoltHandler]
    [MapDelete("/api/community/content/{id:guid}", Tags = ["Community Content"],
        Summary = "Delete content",
        Description = "Soft-deletes content. Validates that the requester owns the content.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteContentRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.DeleteContentAsync(request, ct);
    }
}
