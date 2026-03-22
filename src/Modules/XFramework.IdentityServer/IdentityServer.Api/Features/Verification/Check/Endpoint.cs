using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Verification.Check;

public static class CheckVerificationEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/verifications/check", Tags = ["Verification"],
        Summary = "Check verification status",
        Description = "Checks if a valid (non-expired) verification exists for a credential. Verifications expire after 10 minutes.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CheckVerificationResponse>> Handle(
        CheckVerificationRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.CheckVerificationAsync(request, ct);
    }
}
