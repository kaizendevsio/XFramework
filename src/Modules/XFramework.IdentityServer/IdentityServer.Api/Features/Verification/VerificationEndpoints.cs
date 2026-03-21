using IdentityServer.Api.Features.Verification.Check;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Features.Verification.Create;

namespace IdentityServer.Api.Features.Verification;

/// <summary>
/// Verification feature endpoints aggregator
/// </summary>
public static class VerificationEndpoints
{
    public static void MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateVerification();
        app.MapConfirmVerification();
        app.MapCheckVerification();
    }
}