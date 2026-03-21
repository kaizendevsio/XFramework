using IdentityServer.Api.Features.Credentials.Create;
using IdentityServer.Api.Features.Credentials.Update;

namespace IdentityServer.Api.Features.Credentials;

/// <summary>
/// Credentials feature endpoints aggregator
/// </summary>
public static class CredentialEndpoints
{
    public static void MapCredentialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateCredential();
        app.MapUpdateCredential();
    }
}