using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IdentityServer.Api.Features.Auth.ChangePassword;

/// <summary>
/// Change password endpoint
/// </summary>
public static class ChangePasswordEndpoint
{
    public static void MapChangePassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/change-password", Handle)
            .WithName("ChangePassword")
            .WithTags("Auth")
            .WithOpenApi(op =>
            {
                op.Summary = "Change user password";
                op.Description = "Changes a user's password with optional verification requirement. Uses BCrypt hashing.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Ok<string>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        ChangePasswordRequest request,
        IAuthService authService,
        IValidator<ChangePasswordRequest> validator,
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

        var result = await authService.ChangePasswordAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Password change failed",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Message);
    }
}

/// <summary>
/// Validator for ChangePasswordRequest
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CreadentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        When(x => x.RequireVerificationId, () =>
        {
            RuleFor(x => x.VerificationId)
                .NotEmpty().WithMessage("Verification ID is required when verification is required");
        });
    }
}