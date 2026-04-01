using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.Create;

public static class CreateContentFileEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/content/{contentId:guid}/files", Tags = ["Community Content"],
        Summary = "Attach file to content",
        Description = "Attaches a storage file to content. Validates requester is the content author.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateContentFileVsaRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.CreateContentFileAsync(request, ct);
    }
}
