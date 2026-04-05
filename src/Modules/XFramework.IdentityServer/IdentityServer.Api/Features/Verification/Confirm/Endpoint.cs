using XFramework.Integration.Attributes;
using PatchVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Patch<IdentityServer.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Confirm;

public static class ConfirmVerificationEndpoint
{
    [MapPatch("/api/verifications/{token}", Tags = ["Verification"],
        Summary = "Confirm a verification",
        Description = "Updates a verification status from Pending to Approved when valid token is provided.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<IdentityVerification>> Handle(
        PatchVerificationRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.UpdateVerificationAsync(request, ct);
    }
}
