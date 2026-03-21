using IdentityServer.Api.Features.Auth.Authenticate;
using IdentityServer.Api.Features.Auth.ChangePassword;
using IdentityServer.Api.Features.Auth.VerifyPassword;

namespace IdentityServer.Api.Features.Auth;

/// <summary>
/// Auth feature endpoints aggregator
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthenticate();
        app.MapChangePassword();
        app.MapVerifyPassword();
    }
}