using FluentValidation;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using PatchRequest = XFramework.Domain.Shared.Contracts.Requests.Patch<XFramework.Domain.Shared.Contracts.IdentityCredential>;

namespace IdentityServer.Api.Features.Credentials.Update;

/// <summary>
/// Update credential endpoint
/// </summary>
public static class UpdateCredentialEndpoint
{
    public static void MapUpdateCredential(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/credentials/{id:guid}", Handle)
            .WithName("UpdateCredential")
            .WithTags("Credentials")
            .WithOpenApi(op =>
            {
                op.Summary = "Update an identity credential";
                op.Description = "Updates an identity credential (excluding password, use change-password endpoint for that).";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Ok<IdentityCredential>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        PatchRequest request,
        IAuthService authService,
        IValidator<PatchRequest> validator,
        CancellationToken ct)
    {
        // Set the ID from the route
        request.Model.Id = id;

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

        var result = await authService.UpdateCredentialAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Error updating credential",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Data!);
    }
}

/// <summary>
/// Validator for Patch IdentityCredential request
/// </summary>
public class UpdateCredentialRequestValidator : AbstractValidator<PatchRequest>
{
    public UpdateCredentialRequestValidator()
    {
        RuleFor(x => x.Model.Id)
            .NotEmpty().WithMessage("Credential ID is required");
    }
}