using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.ReleaseTransaction;

/// <summary>
/// Release Transaction endpoint
/// </summary>
public static class ReleaseTransactionEndpoint
{
    public static void MapReleaseTransaction(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/release-transaction", Handle)
            .WithName("ReleaseTransaction")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Release a held transaction";
                op.Description = "Releases a transaction that was previously placed on hold. Moves the amount from on-hold balances to available balances.";
                return op;
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] ReleaseTransactionRequest request,
        [FromServices] IWalletService walletService,
        CancellationToken ct)
    {
        // Basic validation
        if (request.Id == Guid.Empty)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                { "Id", new[] { "Transaction ID is required" } }
            });
        }

        // Call service
        var result = await walletService.ReleaseTransactionAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error releasing transaction",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok();
    }
}