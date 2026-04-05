using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Files.Update;

public static class UpdateIdentityFileEndpoint
{
    [BoltHandler]
    [MapPut("/api/community/identities/{identityId:guid}/files/{fileId:guid}", Tags = ["Community Identity"],
        Summary = "Update identity file",
        Description = "Updates the storage reference for a profile or cover photo. Validates requester owns the identity.")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateIdentityFileRequest request,
        ICommunityService communityService,
        CancellationToken ct)
    {
        return await communityService.UpdateIdentityFileAsync(request, ct);
    }
}
