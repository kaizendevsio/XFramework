using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Create;

public static class CreateCommunityIdentityEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/identities", Tags = ["Community Identities"],
        Summary = "Create a new community identity",
        Description = "Creates a new community identity for a credential",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        CreateCommunityIdentityRequest request,
        ICommunityService communityService,
        CancellationToken ct)
    {
        return await communityService.CreateCommunityIdentityAsync(request, ct);
    }
}
