using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Api.Services;

namespace Wallets.Api.Features.Wallets.Get;

/// <summary>
/// Get Wallet endpoint
/// </summary>
public static class GetWalletEndpoint
{
    public static void MapGetWallet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/{walletId:guid}", Handle)
            .WithName("GetWallet")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Get a wallet by ID";
                op.Description = "Retrieves a wallet by its unique identifier";
                return op;
            })
            .Produces<WalletResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<WalletResponse>, NotFound, ProblemHttpResult>> Handle(
        [FromRoute] Guid walletId,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromServices] IWalletService walletService,
        CancellationToken ct)
    {
        // Call service
        var result = await walletService.GetWalletAsync(walletId, tenantId, ct);

        if (!result.IsSuccess)
        {
            if (result.StatusCode == 404)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Problem(
                title: "Error retrieving wallet",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        // Map to response
        var response = WalletResponse.FromWallet(result.Data!);

        return TypedResults.Ok(response);
    }
}