using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.Transfer;

/// <summary>
/// Transfer Funds endpoint
/// </summary>
public static class TransferEndpoint
{
    public static void MapTransfer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/transfer", Handle)
            .WithName("TransferFunds")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Transfer funds between wallets";
                op.Description = "Transfers funds from one wallet to another. Handles fee deduction based on TransferDeductionType. Automatically creates recipient wallet if it doesn't exist.";
                return op;
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] TransferWalletRequest request,
        [FromServices] IWalletService walletService,
        [FromServices] IValidator<TransferWalletRequest> validator,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return TypedResults.ValidationProblem(errors);
        }

        // Call service
        var result = await walletService.TransferAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error transferring funds",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok();
    }
}