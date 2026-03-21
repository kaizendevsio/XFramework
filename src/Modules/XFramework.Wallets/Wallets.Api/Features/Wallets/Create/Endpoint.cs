using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Api.Services;

namespace Wallets.Api.Features.Wallets.Create;

/// <summary>
/// Create Wallet endpoint
/// </summary>
public static class CreateWalletEndpoint
{
    public static void MapCreateWallet(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets", Handle)
            .WithName("CreateWallet")
            .WithTags("Wallets")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new wallet";
                op.Description = "Creates a new wallet for a credential with the specified wallet type. Automatically generates a unique account number.";
                return op;
            })
            .Produces<WalletResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Created<WalletResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        [FromBody] CreateWalletRequest request,
        [FromServices] IWalletService walletService,
        [FromServices] IValidator<CreateWalletRequest> validator,
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
        var result = await walletService.CreateWalletAsync(
            request.CredentialId,
            request.WalletTypeId,
            request.InitialBalance,
            request.TenantId,
            ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating wallet",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        // Map to response
        var response = WalletResponse.FromWallet(result.Data!);

        return TypedResults.Created($"/api/wallets/{response.Id}", response);
    }
}