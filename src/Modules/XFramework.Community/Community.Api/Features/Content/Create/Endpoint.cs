using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Create;

public static class CreateContentEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/content", Tags = ["Community Content"],
        Summary = "Create new content or comment",
        Description = "Creates a new content post. If ParentContentId is provided, creates a comment on that content and notifies the author.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateContentRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.CreateContentAsync(request, ct);
    }
}
