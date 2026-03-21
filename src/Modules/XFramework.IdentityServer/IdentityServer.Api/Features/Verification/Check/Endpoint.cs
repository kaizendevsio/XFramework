using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IdentityServer.Api.Features.Verification.Check;

/// <summary>
/// Check verification endpoint - Checks if a valid verification exists
/// </summary>
public static class CheckVerificationEndpoint
{
    public static void MapCheckVerification(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/verifications/check", Handle)
            .WithName("CheckVerification")
            .WithTags("Verification")
            .WithOpenApi(op =>
            {
                op.Summary = "Check verification status";
                op.Description = "Checks if a valid (non-expired) verification exists for a credential. Verifications expire after 10 minutes.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Ok<CheckVerificationResponse>, NotFound, ProblemHttpResult>> Handle(
        [FromQuery] Guid credentialId,
        [FromQuery] Guid verificationTypeId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId,
        IAuthService authService,
        CancellationToken ct)
    {
        var request = new CheckVerificationRequest
        {
            CredentialId = credentialId,
            VerificationTypeId = verificationTypeId,
            Metadata = new() { TenantId = tenantId }
        };

        var result = await authService.CheckVerificationAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Error checking verification",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Data!);
    }
}
