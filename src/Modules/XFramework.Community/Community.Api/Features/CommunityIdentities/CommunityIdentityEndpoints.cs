using Community.Api.Features.CommunityIdentities.Create;
using Community.Api.Features.CommunityIdentities.Update;

namespace Community.Api.Features.CommunityIdentities;

/// <summary>
/// Extension methods for registering Community Identity endpoints
/// </summary>
public static class CommunityIdentityEndpoints
{
    /// <summary>
    /// Maps all Community Identity endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapCommunityIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/community/identities")
            .WithTags("Community Identities")
            .WithOpenApi();

        // Map individual endpoints
        app.MapCreateCommunityIdentity();
        app.MapUpdateCommunityIdentity();

        return app;
    }
}