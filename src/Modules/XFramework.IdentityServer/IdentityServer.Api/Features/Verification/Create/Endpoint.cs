using FluentValidation;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using CreateVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Create<XFramework.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Create;

/// <summary>
/// Create verification endpoint - Creates SMS OTP verification
/// </summary>
public static class CreateVerificationEndpoint
{
    public static void MapCreateVerification(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/verifications", Handle)
            .WithName("CreateVerification")
            .WithTags("Verification")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new verification";
                op.Description = "Creates a verification code (SMS OTP) for multi-factor authentication and sends SMS message.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Created<IdentityVerification>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        CreateVerificationRequest request,
        IAuthService authService,
        IValidator<CreateVerificationRequest> validator,
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

        var result = await authService.CreateVerificationAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Error creating verification",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Created($"/api/verifications/{result.Data!.Id}", result.Data);
    }
}

/// <summary>
/// Validator for Create IdentityVerification request
/// </summary>
public class CreateVerificationRequestValidator : AbstractValidator<CreateVerificationRequest>
{
    public CreateVerificationRequestValidator()
    {
        RuleFor(x => x.Model.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Model.VerificationTypeId)
            .NotEmpty().WithMessage("Verification Type ID is required");
    }
}