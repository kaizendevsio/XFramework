using IdentityServer.Api.Features.Auth;
using IdentityServer.Api.Features.Credentials;
using IdentityServer.Api.Features.Files;
using IdentityServer.Api.Features.Verification;

namespace IdentityServer.Api.Features;

/// <summary>
/// Main aggregator for all IdentityServer feature endpoints
/// </summary>
public static class IdentityServerFeatureEndpoints
{
    public static void MapIdentityServerFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapCredentialEndpoints();
        app.MapVerificationEndpoints();
        app.MapFileEndpoints();
    }
}