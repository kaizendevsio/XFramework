using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;

namespace IdentityServer.Api.Features.Verification.Confirm;

/// <summary>
/// Confirm verification endpoint - Updates verification status from Pending to Approved
/// </summary>
public static class ConfirmVerificationEndpoint
{
    public static void MapConfirmVerification(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/verifications/{token}", Handle)
            .WithName("ConfirmVerification")
            .WithTags("Verification")
            .WithOpenApi(op =>
            {
                op.Summary = "Confirm a verification";
                op.Description = "Updates a verification status from Pending to Approved when valid token is provided.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Ok<IdentityVerification>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        string token,
        IAuthService authService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            var errors = new Dictionary<string, string[]>
            {
                { "token", new[] { "Token is required" } }
            };
            return TypedResults.ValidationProblem(errors);
        }

        var request = new Patch<IdentityVerification>(new IdentityVerification { Token = token });

        var result = await authService.UpdateVerificationAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Error confirming verification",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Data!);
    }
}