using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Api.Services;

namespace Wallets.Api.Features.Wallets.GetByCredential;

/// <summary>
/// Get Wallets by Credential endpoint
/// </summary>
public static class GetWalletsByCredentialEndpoint
{
    public static void MapGetWalletsByCredential(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/credential/{credentialId:guid}", Handle)
            .WithName("GetWalletsByCredential")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Get all wallets for a credential";
                op.Description = "Retrieves all wallets associated with a specific credential ID";
                return op;
            })
            .Produces<List<WalletResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<List<WalletResponse>>, ProblemHttpResult>> Handle(
        [FromRoute] Guid credentialId,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromServices] IWalletService walletService,
        CancellationToken ct)
    {
        // Call service
        var result = await walletService.GetWalletsByCredentialAsync(credentialId, tenantId, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving wallets",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        // Map to response
        var response = result.Data!.Select(WalletResponse.FromWallet).ToList();

        return TypedResults.Ok(response);
    }
}