using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.WithdrawFunds;

/// <summary>
/// Withdraw Funds (Decrement) endpoint
/// </summary>
public static class WithdrawFundsEndpoint
{
    public static void MapWithdrawFunds(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/withdraw-funds", Handle)
            .WithName("WithdrawFunds")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Withdraw funds from a wallet";
                op.Description = "Decrements (subtracts from) a wallet's balance. Supports both immediate and on-hold decrements. Validates sufficient available balance.";
                return op;
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] DecrementWalletRequest request,
        [FromServices] IWalletService walletService,
        [FromServices] IValidator<DecrementWalletRequest> validator,
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
        var result = await walletService.DecrementBalanceAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error withdrawing funds",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok();
    }
}