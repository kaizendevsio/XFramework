using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.Convert;

/// <summary>
/// Convert Currency endpoint
/// </summary>
public static class ConvertEndpoint
{
    public static void MapConvert(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/convert", Handle)
            .WithName("ConvertCurrency")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Convert funds between wallet types";
                op.Description = "Converts funds from one wallet type to another for the same credential. Handles fee deduction based on TransferDeductionType. Automatically creates target wallet if it doesn't exist.";
                return op;
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] ConvertWalletRequest request,
        [FromServices] IWalletService walletService,
        [FromServices] IValidator<ConvertWalletRequest> validator,
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
        var result = await walletService.ConvertWalletAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error converting currency",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok();
    }
}