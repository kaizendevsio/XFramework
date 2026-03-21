using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.AddFunds;

/// <summary>
/// Add Funds (Increment) endpoint
/// </summary>
public static class AddFundsEndpoint
{
    public static void MapAddFunds(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/add-funds", Handle)
            .WithName("AddFunds")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Add funds to a wallet";
                op.Description = "Increments (adds to) a wallet's balance. Supports both immediate and on-hold increments. Automatically creates wallet if WalletTypeId is provided and wallet doesn't exist.";
                return op;
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] IncrementWalletRequest request,
        [FromServices] IWalletService walletService,
        [FromServices] IValidator<IncrementWalletRequest> validator,
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
        var result = await walletService.IncrementBalanceAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error adding funds",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok();
    }
}