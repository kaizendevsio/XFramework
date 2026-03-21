using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IdentityServer.Api.Features.Auth.VerifyPassword;

/// <summary>
/// Verify password endpoint
/// </summary>
public static class VerifyPasswordEndpoint
{
    public static void MapVerifyPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verify-password", Handle)
            .WithName("VerifyPassword")
            .WithTags("Auth")
            .WithOpenApi(op =>
            {
                op.Summary = "Verify user password";
                op.Description = "Verifies a password against stored credential using BCrypt.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Ok<bool>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        VerifyPasswordRequest request,
        IAuthService authService,
        IValidator<VerifyPasswordRequest> validator,
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

        var result = await authService.VerifyPasswordAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Password verification failed",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Data);
    }
}

/// <summary>
/// Validator for VerifyPasswordRequest
/// </summary>
public class VerifyPasswordRequestValidator : AbstractValidator<VerifyPasswordRequest>
{
    public VerifyPasswordRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}