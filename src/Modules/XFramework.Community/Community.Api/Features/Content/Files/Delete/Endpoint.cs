using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.Delete;

public static class DeleteContentFileEndpoint
{
    [BoltHandler]
    [MapDelete("/api/community/content/{contentId:guid}/files/{fileId:guid}", Tags = ["Community Content"],
        Summary = "Remove file from content",
        Description = "Soft-deletes a file attachment. Validates requester is the content author.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteContentFileRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.DeleteContentFileAsync(request, ct);
    }
}
