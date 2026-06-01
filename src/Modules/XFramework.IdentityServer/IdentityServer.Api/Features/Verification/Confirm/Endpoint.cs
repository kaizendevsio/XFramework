using PatchVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Patch<IdentityServer.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Confirm;

public static class ConfirmVerificationEndpoint
{
    public static IEndpointRouteBuilder MapConfirmVerificationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/verifications/{token}", ConfirmFromRoute)
            .WithTags("Verification")
            .WithSummary("Confirm a verification")
            .WithDescription("Updates a verification status from Pending to Approved when a valid, non-expired token is provided.")
            .ExcludeFromDescription();

        return app;
    }

    public static async Task<Result<IdentityVerification>> Handle(
        PatchVerificationRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.UpdateVerificationAsync(request, ct);
    }

    private static async Task<IResult> ConfirmFromRoute(
        string token,
        IAuthService authService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest(new { message = "Verification token is required" });
        }

        var request = new PatchVerificationRequest(new IdentityVerification { Token = token });
        var result = await Handle(request, authService, ct);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Json(result, statusCode: result.StatusCode);
    }
}
