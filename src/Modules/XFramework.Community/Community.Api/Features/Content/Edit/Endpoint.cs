using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Edit;

public static class EditContentEndpoint
{
    [BoltHandler]
    [MapPatch("/api/community/content/{id:guid}", Tags = ["Community Content"],
        Summary = "Edit content",
        Description = "Updates content text and/or title. Validates that the requester owns the content.")]
    public static async Task<Result<CmdResponse>> Handle(
        EditContentRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.EditContentAsync(request, ct);
    }
}
