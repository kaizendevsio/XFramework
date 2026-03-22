using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Update;

public static class UpdateCommunityIdentityEndpoint
{
    [MapPatch("/api/community/identities/{id:guid}", Tags = ["Community Identities"],
        Summary = "Update an existing community identity",
        Description = "Updates a community identity by ID",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateCommunityIdentityRequest request,
        ICommunityService communityService,
        CancellationToken ct)
    {
        return await communityService.UpdateCommunityIdentityAsync(request, ct);
    }
}
