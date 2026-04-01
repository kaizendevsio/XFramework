using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.GetList;

public static class GetContentFilesEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/content/{contentId:guid}/files", Tags = ["Community Content"],
        Summary = "List content files",
        Description = "Returns all file attachments for a given content item.")]
    public static async Task<Result<List<ContentFileResponse>>> Handle(
        GetContentFilesRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.GetContentFilesAsync(request, ct);
    }
}
